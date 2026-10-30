using Canyon.Database.Entities;
using Canyon.Game.Database;
using Canyon.Game.Sockets.Game.Packets;
using Canyon.Game.States.Items;
using Canyon.Game.States.User;
using System.Collections.Concurrent;
using System.Threading;

namespace Canyon.Game.States
{
    public sealed class Trade
    {
        private const int MaxTradeItems = 20;
        private const int MaxTradeMoney = 1000000000;
        private const int MaxTradeEmoney = 1000000000;
        private const ulong MaxInventoryMoney = 10_000_000_000UL;

        private readonly ConcurrentDictionary<uint, Item> itemsPlayer1 = new();
        private readonly ConcurrentDictionary<uint, Item> itemsPlayer2 = new();
        private readonly SemaphoreSlim tradeLock = new(1, 1);

        private uint money1, money2;
        private uint emoney1, emoney2;

        private bool accept1, accept2;
        private bool isClosed;

        public Trade(Character p1, Character p2)
        {
            User1 = p1;
            User2 = p2;

            User1.Trade = this;
            User2.Trade = this;
        }

        public Character User1 { get; }
        public Character User2 { get; }

        public bool Accepted => accept1 && accept2;

        public bool ContainsItem(uint idItem)
        {
            return itemsPlayer1.ContainsKey(idItem) || itemsPlayer2.ContainsKey(idItem);
        }

        public async Task<bool> AddItemAsync(uint idItem, Character sender)
        {
            await tradeLock.WaitAsync();
            try
            {
                if (isClosed)
                {
                    return false;
                }

                if (sender.Identity != User1.Identity
                    && sender.Identity != User2.Identity)
                {
                    return false;
                }

                Character target = sender.Identity == User1.Identity ? User2 : User1;
                ConcurrentDictionary<uint, Item> items = User1.Identity == sender.Identity ? itemsPlayer1 : itemsPlayer2;

                Item item = sender.UserPackage[idItem];
                if (item == null)
                {
                    await sender.SendAsync(StrNotToTrade);
                    await sender.SendAsync(RemoveMsg(idItem));
                    return false;
                }

                if (items.ContainsKey(idItem))
                {
                    await sender.SendAsync(RemoveMsg(idItem));
                    return false;
                }

                if (!IsValidTradeItem(item, sender, target))
                {
                    await sender.SendAsync(StrNotToTrade);
                    await sender.SendAsync(RemoveMsg(idItem));
                    return false;
                }

                if (items.Count >= MaxTradeItems)
                {
                    await sender.SendAsync(StrTradeSashFull);
                    await sender.SendAsync(RemoveMsg(idItem));
                    return false;
                }

                if (!target.UserPackage.IsPackSpare(items.Count + 1))
                {
                    await target.SendAsync(StrTradeYourBagIsFull);
                    await sender.SendAsync(StrTradeTargetBagIsFull);
                    await sender.SendAsync(RemoveMsg(idItem));
                    return false;
                }

                if (!items.TryAdd(item.Identity, item))
                {
                    await sender.SendAsync(RemoveMsg(idItem));
                    return false;
                }

                accept1 = false;
                accept2 = false;

                await target.SendAsync(new MsgItemInfo(item, MsgItemInfo.ItemMode.Trade));
                if (item.Quench != null)
                {
                    await item.Quench.SendToAsync(target);
                }
                return true;
            }
            finally
            {
                tradeLock.Release();
            }
        }

        public async Task<bool> AddMoneyAsync(uint amount, Character sender)
        {
            await tradeLock.WaitAsync();
            try
            {
                if (isClosed)
                {
                    return false;
                }

                if (sender.Identity != User1.Identity
                    && sender.Identity != User2.Identity)
                {
                    return false;
                }

                Character target = sender.Identity == User1.Identity ? User2 : User1;
                if (amount > MaxTradeMoney)
                {
                    await sender.SendAsync(string.Format(StrTradeMuchMoney, MaxTradeMoney));
                    await SendCloseInternalAsync();
                    return false;
                }

                if (sender.Silvers < amount)
                {
                    await sender.SendAsync(StrNotEnoughMoney);
                    await SendCloseInternalAsync();
                    return false;
                }

                if (sender.Identity == User1.Identity)
                {
                    money1 = amount;
                }
                else
                {
                    money2 = amount;
                }

                accept1 = false;
                accept2 = false;

                await target.SendAsync(new MsgTrade
                {
                    Data = amount,
                    Action = MsgTrade.TradeAction.ShowMoney
                });
                return true;
            }
            finally
            {
                tradeLock.Release();
            }
        }

        public async Task<bool> AddEmoneyAsync(uint amount, Character sender)
        {
            await tradeLock.WaitAsync();
            try
            {
                if (isClosed)
                {
                    return false;
                }

                if (sender.Identity != User1.Identity
                    && sender.Identity != User2.Identity)
                {
                    return false;
                }

                Character target = sender.Identity == User1.Identity ? User2 : User1;

                if (amount > MaxTradeEmoney)
                {
                    await sender.SendAsync(string.Format(StrTradeMuchEmoney, MaxTradeEmoney));
                    await SendCloseInternalAsync();
                    return false;
                }

                if (sender.ConquerPoints < amount)
                {
                    await sender.SendAsync(StrNotEnoughMoney);
                    await SendCloseInternalAsync();
                    return false;
                }

                if (sender.Identity == User1.Identity)
                {
                    emoney1 = amount;
                }
                else
                {
                    emoney2 = amount;
                }

                accept1 = false;
                accept2 = false;

                await target.SendAsync(new MsgTrade
                {
                    Data = amount,
                    Action = MsgTrade.TradeAction.ShowConquerPoints
                });
                return true;
            }
            finally
            {
                tradeLock.Release();
            }
        }

        public async Task AcceptAsync(uint acceptId)
        {
            await tradeLock.WaitAsync();
            try
            {
                if (isClosed)
                {
                    return;
                }

                if (acceptId == User1.Identity)
                {
                    accept1 = true;
                    await User2.SendAsync(new MsgTrade
                    {
                        Action = MsgTrade.TradeAction.Accept,
                        Data = acceptId
                    });
                }
                else if (acceptId == User2.Identity)
                {
                    accept2 = true;
                    await User1.SendAsync(new MsgTrade
                    {
                        Action = MsgTrade.TradeAction.Accept,
                        Data = acceptId
                    });
                }
                else
                {
                    return;
                }

                if (!Accepted)
                {
                    return;
                }

                bool success1 = itemsPlayer1.Values.All(x => IsValidTradeItem(x, User1, User2));
                bool success2 = itemsPlayer2.Values.All(x => IsValidTradeItem(x, User2, User1));

                bool success = success1 && success2;

                if (User1.Client == null || User2.Client == null || User1.MapIdentity != User2.MapIdentity)
                {
                    success = false;
                }

                if (!User1.UserPackage.IsPackSpare(itemsPlayer2.Count))
                {
                    success = false;
                }

                if (!User2.UserPackage.IsPackSpare(itemsPlayer1.Count))
                {
                    success = false;
                }

                if (money1 > User1.Silvers || emoney1 > User1.ConquerPoints)
                {
                    success = false;
                }

                if (money2 > User2.Silvers || emoney2 > User2.ConquerPoints)
                {
                    success = false;
                }

                if (User2.Silvers + money1 > MaxInventoryMoney || User1.Silvers + money2 > MaxInventoryMoney)
                {
                    success = false;
                }

                if ((ulong)User2.ConquerPoints + emoney1 > int.MaxValue || (ulong)User1.ConquerPoints + emoney2 > int.MaxValue)
                {
                    success = false;
                }

                if (!success)
                {
                    await SendCloseInternalAsync();
                    return;
                }

                isClosed = true;
                User1.Trade = null;
                User2.Trade = null;

                var dbTrade = new DbTrade
                {
                    Type = DbTrade.TradeType.Trade,
                    UserIpAddress = User1.Client.IpAddress,
                    UserMacAddress = User1.Client.MacAddress,
                    TargetIpAddress = User2.Client.IpAddress,
                    TargetMacAddress = User2.Client.MacAddress,
                    MapIdentity = User1.MapIdentity,
                    TargetEmoney = emoney2,
                    TargetMoney = money2,
                    UserEmoney = emoney1,
                    UserMoney = money1,
                    TargetIdentity = User2.Identity,
                    UserIdentity = User1.Identity,
                    TargetX = User2.X,
                    TargetY = User2.Y,
                    UserX = User1.X,
                    UserY = User1.Y,
                    Timestamp = DateTime.Now
                };
                await ServerDbContext.SaveAsync(dbTrade);

                await SendClosePacketsAsync();

                if (money1 > 0 && await User1.SpendMoneyAsync((int)money1))
                {
                    await User2.AwardMoneyAsync((int)money1);
                }

                if (money2 > 0 && await User2.SpendMoneyAsync((int)money2))
                {
                    await User1.AwardMoneyAsync((int)money2);
                }

                if (emoney1 > 0 && await User1.SpendConquerPointsAsync((int)emoney1))
                {
                    await User2.AwardConquerPointsAsync((int)emoney1);
                }

                if (emoney2 > 0 && await User2.SpendConquerPointsAsync((int)emoney2))
                {
                    await User1.AwardConquerPointsAsync((int)emoney2);
                }

                var dbItemsRecordTrack = new List<DbTradeItem>(41);
                foreach (Item item in itemsPlayer1.Values)
                {
                    if (!IsValidTradeItem(item, User1, User2))
                    {
                        continue;
                    }

                    if (item.IsSuperFlag())
                    {
                        await item.ClearSuperFlagAsync();
                    }

                    await User1.UserPackage.RemoveFromInventoryAsync(item, UserPackage.RemovalType.RemoveAndDisappear);
                    await User2.UserPackage.AddItemAsync(item);

                    dbItemsRecordTrack.Add(new DbTradeItem
                    {
                        TradeIdentity = dbTrade.Identity,
                        SenderIdentity = User1.Identity,
                        ItemIdentity = item.Identity,
                        Itemtype = item.Type,
                        Chksum = (uint)item.ToJson().GetHashCode(),
                        JsonData = item.ToJson()
                    });
                }

                foreach (Item item in itemsPlayer2.Values)
                {
                    if (!IsValidTradeItem(item, User2, User1))
                    {
                        continue;
                    }

                    if (item.IsSuperFlag())
                    {
                        await item.ClearSuperFlagAsync();
                    }

                    await User2.UserPackage.RemoveFromInventoryAsync(item, UserPackage.RemovalType.RemoveAndDisappear);
                    await User1.UserPackage.AddItemAsync(item);

                    dbItemsRecordTrack.Add(new DbTradeItem
                    {
                        TradeIdentity = dbTrade.Identity,
                        SenderIdentity = User2.Identity,
                        ItemIdentity = item.Identity,
                        Itemtype = item.Type,
                        Chksum = (uint)item.ToJson().GetHashCode(),
                        JsonData = item.ToJson()
                    });
                }

                if (dbItemsRecordTrack.Count > 0)
                {
                    await ServerDbContext.SaveRangeAsync(dbItemsRecordTrack);
                }

                await User1.SendAsync(StrTradeSuccess);
                await User2.SendAsync(StrTradeSuccess);
            }
            finally
            {
                tradeLock.Release();
            }
        }

        public async Task SendCloseAsync()
        {
            await tradeLock.WaitAsync();
            try
            {
                if (isClosed)
                {
                    return;
                }

                await SendCloseInternalAsync();
            }
            finally
            {
                tradeLock.Release();
            }
        }

        private async Task SendCloseInternalAsync()
        {
            isClosed = true;
            User1.Trade = null;
            User2.Trade = null;
            await SendClosePacketsAsync();
        }

        private async Task SendClosePacketsAsync()
        {
            await User1.SendAsync(new MsgTrade
            {
                Action = MsgTrade.TradeAction.Fail,
                Data = User2.Identity
            });

            await User2.SendAsync(new MsgTrade
            {
                Action = MsgTrade.TradeAction.Fail,
                Data = User1.Identity
            });
        }

        private static bool IsValidTradeItem(Item item, Character sender, Character target)
        {
            if (item == null || sender.UserPackage[item.Identity] == null)
            {
                return false;
            }

            if (!sender.IsPm() && !target.IsPm())
            {
                if (item.IsMonopoly() || item.IsBound)
                {
                    return false;
                }

                if (item.IsSuspicious())
                {
                    return false;
                }
            }

            if (!sender.IsGm() && !target.IsGm())
            {
                if (item.IsLocked() && !sender.IsValidTradePartner(target.Identity))
                {
                    return false;
                }
            }

            if (item.SyndicateIdentity != 0)
            {
                return false;
            }

            if (sender.Booth?.QueryItem(item.Identity) != null)
            {
                return false;
            }

            return true;
        }

        private MsgTrade RemoveMsg(uint id)
        {
            return new MsgTrade
            {
                Action = MsgTrade.TradeAction.AddItemFail,
                Data = id
            };
        }
    }
}
