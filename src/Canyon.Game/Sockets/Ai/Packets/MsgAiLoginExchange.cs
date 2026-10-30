using Canyon.Game.Services.Managers;
using Canyon.Game.States;
using Canyon.Network.Packets.Ai;
using System.Security.Cryptography;
using System.Text;

namespace Canyon.Game.Sockets.Ai.Packets
{
    public sealed class MsgAiLoginExchange : MsgAiLoginExchange<AiClient>
    {
        private static readonly ILogger logger = LogFactory.CreateLogger<MsgAiLoginExchange>();

        public override async Task ProcessAsync(AiClient client)
        {
            logger.LogInformation($"Received auth data from {client.GUID}");
            if (client.Stage != AiClient.ConnectionStage.AwaitingAuth)
            {
                logger.LogWarning($"This Npc Server is already signed in!");
                await client.SendAsync(new MsgAiLoginExchangeEx
                {
                    Result = MsgAiLoginExchangeEx<AiClient>.AiLoginResult.AlreadySignedIn
                });
                return;
            }

            string expectedUser = ServerConfiguration.Configuration.Ai.Username ?? string.Empty;
            string expectedPass = ServerConfiguration.Configuration.Ai.Password ?? string.Empty;
            bool validUser = !string.IsNullOrEmpty(UserName)
                && CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(UserName),
                    Encoding.UTF8.GetBytes(expectedUser));
            bool validPass = !string.IsNullOrEmpty(Password)
                && CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(Password),
                    Encoding.UTF8.GetBytes(expectedPass));

            if (!validUser || !validPass)
            {
                logger.LogError($"Invalid AI username or password from [{client.IpAddress}]!");
                await client.SendAsync(new MsgAiLoginExchangeEx
                {
                    Result = MsgAiLoginExchangeEx<AiClient>.AiLoginResult.InvalidPassword
                }, () =>
                {
                    client.Disconnect();
                    return Task.CompletedTask;
                });
                return;
            }

            if (NpcServer.NpcClient != null
                && NpcServer.NpcClient.Socket.Connected
                && !NpcServer.NpcClient.GUID.Equals(client.GUID))
            {
                logger.LogWarning($"NPC Server is already connected...");
                await client.SendAsync(new MsgAiLoginExchangeEx
                {
                    Result = MsgAiLoginExchangeEx<AiClient>.AiLoginResult.AlreadyBound
                }, () =>
                {
                    client.Disconnect();
                    return Task.CompletedTask;
                });
                return;
            }

            client.Stage = AiClient.ConnectionStage.Authenticated;
            NpcServer.SetAuthenticatedClient(client);
            logger.LogWarning($"NPC Server login OK...");

            Kernel.Services.Processor.Queue(0, () =>
            {
                foreach (var mob in RoleManager.QueryRoles(x => x is Monster monster && monster.GeneratorId != 0))
                {
                    mob.QueueAction(mob.LeaveMapAsync);
                }
                return Task.CompletedTask;
            });

            await client.SendAsync(new MsgAiLoginExchangeEx
            {
                Result = MsgAiLoginExchangeEx<AiClient>.AiLoginResult.Success
            });

            var players = RoleManager.QueryUserSet();
            foreach (var player in players)
            {
                await BroadcastNpcMsgAsync(new MsgAiPlayerLogin(player));
            }
        }
    }
}
