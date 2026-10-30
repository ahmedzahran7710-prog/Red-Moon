using Canyon.Login.Managers;
using Canyon.Login.Repositories;
using Canyon.Login.States;
using Canyon.Network.Packets.Login;
using Canyon.Shared;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace Canyon.Login.Sockets.Game.Packets
{
    public sealed class MsgAccServerAuth : MsgAccServerAuth<Realm>
    {
        private static readonly ILogger logger = LogFactory.CreateLogger<MsgAccServerAuth>();

        public override async Task ProcessAsync(Realm client)
        {
            var realmData = await RealmsRepository.FindByIdAsync(Data.RealmId.ToString());
            if (realmData == null)
            {
                logger.LogWarning("Realm [{}] does not exist", Data.RealmId);
                await DenyWithMessageAsync(client, MsgAccServerAuthEx.REALM_DOES_NOT_EXIST);
                return;
            }

            if (!await RealmsRepository.ValidateRealmAsync(Guid.Parse(Data.RealmId), Data.Username, Data.Password))
            {
                logger.LogWarning("[{}] Invalid username or password", realmData.RealmName);
                await DenyWithMessageAsync(client, MsgAccServerAuthEx.INVALID_USERNAME_PASSWORD);
                return;
            }

#if DEBUG
            if (!IsAllowedDebugAddress(realmData.GameIPAddress))
            {
                logger.LogWarning("DEBUG mode blocked external realm IPAddress [{}].", realmData.GameIPAddress);
                await DenyWithMessageAsync(client, MsgAccServerAuthEx.DEBUG_MODE);
                return;
            }
#else
            // TODO: Investigate why this is needed
            // if (!realmData.GameIPAddress.Equals(client.IpAddress))
            // {
            //     logger.LogWarning("[{}] realm IPAddress [{}] do not match target IPAddress [{}]", realmData.RealmName, client.IpAddress, realmData.GameIPAddress);
            //     await DenyWithMessageAsync(client, MsgAccServerAuthEx.UNAUTHORIZED_IP_ADDRESS);
            //     return;
            // }
#endif

            var oldRealm = RealmManager.GetRealm(realmData.RealmID);
            if (oldRealm != null)
            {
                oldRealm.Disconnect();
                logger.LogWarning("Duplicated login for realm [{}] from [{}]", realmData.RealmName, client.IpAddress);
            }

            client.Data = realmData;
            logger.LogInformation("Realm [{}] has connected", realmData.RealmName);
            RealmManager.AddRealm(client);
            await client.SendAsync(new MsgAccServerAuthEx
            {
                Data = new MsgAccServerAuthEx<Realm>.AuthExData
                {
                    RealmId = Data.RealmId,
                    ResponseStatus = MsgAccServerAuthEx.SUCCESS
                }
            });
        }

        private Task DenyWithMessageAsync(Realm actor, int message)
        {
            return actor.SendAsync(new MsgAccServerAuthEx
            {
                Data = new MsgAccServerAuthEx<Realm>.AuthExData
                {
                    RealmId = Data.RealmId,
                    ResponseStatus = message
                }
            },
            () =>
            {
                actor.Disconnect();
                return Task.CompletedTask;
            });
        }

        private static bool IsAllowedDebugAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return false;
            }

            if (ipAddress.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!IPAddress.TryParse(ipAddress.Trim(), out IPAddress parsed))
            {
                return false;
            }

            if (IPAddress.IsLoopback(parsed))
            {
                return true;
            }

            if (parsed.IsIPv4MappedToIPv6)
            {
                parsed = parsed.MapToIPv4();
            }

            if (parsed.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            byte[] octets = parsed.GetAddressBytes();
            return octets[0] == 10
                || octets[0] == 127
                || (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31)
                || (octets[0] == 192 && octets[1] == 168)
                || octets[0] == 25   // Hamachi VPN
                || octets[0] == 26   // Radmin VPN
                || (octets[0] == 100 && octets[1] >= 64 && octets[1] <= 127); // Tailscale / CGNAT
        }
    }
}