using Canyon.Database.Entities;
using Canyon.Game.Database;
using Canyon.Game.Database.Repositories;
using Canyon.Game.Services.Managers;
using Canyon.Game.Sockets.Game.Packets;
using Canyon.Game.States.Items;
using Canyon.Game.States.User;
using System.Collections.Concurrent;

namespace Canyon.Game.States.Mails
{
    public sealed class MailBox
    {
        public const int PageSize = 7;

        private readonly TimeOut checkMailsTimer = new(60);
        private readonly Character user;
        private readonly ConcurrentDictionary<uint, MailMessage> emails = new();
        private readonly SemaphoreSlim mailLock = new(1, 1);

        public MailBox(Character user)
        {
            this.user = user;
        }

        private async Task QueryAsync()
        {
            var mails = await MailRepository.GetAsync(user.Identity);
            foreach (var mail in mails)
            {
                if (!emails.ContainsKey(mail.Id))
                {
                    emails.TryAdd(mail.Id, new MailMessage(mail));
                }
            }
        }

        public async Task InitializeAsync()
        {
            await QueryAsync();
            if (emails.Count > 0)
            {
                await NotifyAsync();
            }
        }

        public static async Task<bool> SendAsync(uint target, string senderName, string header, string body, uint expiration,
            ulong money = 0, uint emoney = 0, bool emoneyMono = false, uint itemId = 0, uint itemType = 0, uint action = 0)
        {
            DbCharacter dbTarget = await CharacterRepository.FindByIdentityAsync(target);
            if (dbTarget == null)
            {
                return false; // target not found
            }

            DbMail mail = new()
            {
                ReceiverId = target,
                SenderName = senderName,
                Title = header,
                Content = body,
                Action = action,
                Money = money,
                ConquerPoints = emoney,
                ExpirationDate = expiration,
                ItemId = itemId,
                EmoneyRecordType = (byte)(emoneyMono ? 1 : 0)
            };

            if (!await ServerDbContext.SaveAsync(mail))
            {
                return false;
            }

            Character targetUser = RoleManager.GetUser(target);
            if (targetUser != null)
            {
                await targetUser.MailBox.QueryAsync();
                await targetUser.MailBox.NotifyAsync();
            }
            return true;
        }

        public async Task SendListAsync(int page)
        {
            int now = UnixTimestamp.Now;
            int from = page;
            MsgMailList msg = new();
            msg.Page = page;
            msg.MaxPages = (ushort)Math.Ceiling(emails.Values.Count / (double)PageSize);
            foreach (var mail in emails.Values
                .Where(x => !x.HasExpired)
                .OrderByDescending(x => x.Order).ThenBy(x => x.Expiration)
                .Skip(from)
                .Take(PageSize))
            {
                msg.MailList.Add(new MsgMailList.MailListStruct
                {
                    EmailIdentity = mail.Identity,
                    SenderName = mail.SenderName,
                    Header = mail.Title[..Math.Min(mail.Title.Length, 32)],
                    Money = (uint)mail.Money,
                    ConquerPoints = mail.ConquerPoints,
                    ItemType = mail.Item,
                    Timestamp = (int)Math.Max(0, mail.Expiration - now),
                    HasAttachment = mail.HasClaimedItem && mail.HasClaimedAction ? 1 : 0
                });
            }
            await user.SendAsync(msg);
        }

        public async Task SendMessageAsync(uint idMail)
        {
            if (!emails.TryGetValue(idMail, out var mail))
            {
                return;
            }

            await user.SendAsync(new MsgMailContent
            {
                Data = idMail,
                Content = mail.Content
            });
        }

        public async Task DeleteMessageAsync(uint idMail)
        {
            await mailLock.WaitAsync();
            try
            {
                if (!emails.TryRemove(idMail, out var mail))
                {
                    return;
                }

                await mail.DeleteAsync();
            }
            finally
            {
                mailLock.Release();
            }
        }

        public async Task ClaimMoneyAsync(uint idMail)
        {
            await mailLock.WaitAsync();
            try
            {
                if (!emails.TryGetValue(idMail, out var mail) || mail.HasExpired || mail.HasClaimedMoney)
                {
                    return;
                }

                ulong amount = Math.Min(mail.Money, 10_000_000_000UL);
                if (user.Silvers + amount > 10_000_000_000UL)
                {
                    return;
                }

                mail.Money = 0;
                await mail.SaveAsync();
                await user.AwardMoneyAsync((long)amount);
            }
            finally
            {
                mailLock.Release();
            }
        }

        public async Task ClaimConquerPointsAsync(uint idMail)
        {
            await mailLock.WaitAsync();
            try
            {
                if (!emails.TryGetValue(idMail, out var mail) || mail.HasExpired || mail.HasClaimedConquerPoints)
                {
                    return;
                }

                int amount = (int)Math.Min(mail.ConquerPoints, (uint)int.MaxValue);
                if (mail.IsEmoneyMono)
                {
                    if ((long)user.ConquerPointsBound + amount > int.MaxValue)
                    {
                        return;
                    }
                    mail.ConquerPoints = 0;
                    await mail.SaveAsync();
                    await user.AwardBoundConquerPointsAsync(amount);
                }
                else
                {
                    if ((long)user.ConquerPoints + amount > int.MaxValue)
                    {
                        return;
                    }
                    mail.ConquerPoints = 0;
                    await mail.SaveAsync();
                    await user.AwardConquerPointsAsync(amount);
                }
            }
            finally
            {
                mailLock.Release();
            }
        }

        public async Task ClaimActionAsync(uint idMail)
        {
            await mailLock.WaitAsync();
            try
            {
                if (!emails.TryGetValue(idMail, out var mail) || mail.HasExpired)
                {
                    return;
                }

                await ClaimActionInternalAsync(mail);
            }
            finally
            {
                mailLock.Release();
            }
        }

        private async Task ClaimActionInternalAsync(MailMessage mail)
        {
            if (mail == null || mail.HasClaimedAction)
            {
                return;
            }

            uint actionId = mail.Action;
            mail.Action = 0;
            await mail.SaveAsync();
            await GameAction.ExecuteActionAsync(actionId, user, null, null, null);
        }

        public async Task ClaimItemAsync(uint idMail)
        {
            await mailLock.WaitAsync();
            try
            {
                if (!emails.TryGetValue(idMail, out var mail) || mail.HasExpired || mail.HasClaimedItem)
                {
                    return;
                }

                DbItem dbItem = await ItemRepository.GetByIdAsync(mail.Item);
                if (dbItem == null || (dbItem.PlayerId != 0 && dbItem.Position != (int)Item.ItemPosition.Auction))
                {
                    mail.Item = 0;
                    await mail.SaveAsync();
                    return;
                }

                Item item = new(user);
                if (!await item.CreateAsync(dbItem))
                {
                    return;
                }

                if (!user.UserPackage.IsPackSpare((int)item.AccumulateNum, item.Type))
                {
                    return;
                }

                mail.Item = 0;
                await mail.SaveAsync();

                await user.UserPackage.AddItemAsync(item);
                await ClaimActionInternalAsync(mail);
            }
            finally
            {
                mailLock.Release();
            }
        }

        public Task NotifyAsync()
        {
            return user.SendAsync(new MsgMailNotify
            {
                Action = MsgMailNotify.MailNotification.Notification
            });
        }

        public async Task OnTimerAsync()
        {
            if (!checkMailsTimer.ToNextTime())
            {
                return;
            }

            await QueryAsync();
        }
    }
}
