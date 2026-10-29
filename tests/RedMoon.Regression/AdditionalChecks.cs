using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using RedMoon.GameServer.Structures.Qualifier;
using RedMoon.Core.Security;

internal static partial class Program
{
    private const string RepairScope = "Repaired source component regression; not an end-to-end client test.";
    private static string Rejects(Action action)
    {
        try { action(); return "ACCEPTED"; }
        catch (ArgumentException) { return "REJECTED"; }
    }
    private static void AdditionalChecks()
    {
        Check("PACKET-03-CP1252-roundtrip", "caf\u00e9", () =>
        {
            var p = new ProbePacket(16); p.WriteString("caf\u00e9", 8, 4); return p.ReadString(8, 4);
        }, RepairScope);
        Check("PACKET-04-UTF8-whole-character-truncation", "\u00e9A,A1B2C3D4", () =>
        {
            var p = new ProbePacket(16); p.WriteUInt(0xA1B2C3D4, 7); p.WriteString("\u00e9A\u00e9", 3, 4, Encoding.UTF8);
            return p.ReadString(3, 4, Encoding.UTF8) + "," + p.ReadUInt(7).ToString("X8");
        }, RepairScope);
        Check("PACKET-05-first-character-does-not-fit", "0,A1B2C3D4", () =>
        {
            var p = new ProbePacket(16); p.WriteUInt(0xA1B2C3D4, 5); p.WriteString("\u00e9", 1, 4, Encoding.UTF8);
            return p.ReadByte(4) + "," + p.ReadUInt(5).ToString("X8");
        }, RepairScope);
        Check("PACKET-06-surrogate-not-split", "0", () =>
        {
            var p = new ProbePacket(16); p.WriteString("\ud83c\udf19", 3, 4, Encoding.UTF8); return p.ReadByte(4).ToString();
        }, RepairScope);
        Check("PACKET-07-read-bounds", "REJECTED", () => Rejects(() => new ProbePacket(4).ReadUInt(1)), RepairScope);
        Check("PACKET-08-write-bounds", "REJECTED", () => Rejects(() => new ProbePacket(4).WriteUInt(1, -1)), RepairScope);
        Check("PACKET-09-array-copies-data", "01-02-03-04", () =>
        {
            var p = new ProbePacket(16); p.WriteArray(new byte[] { 1, 2, 3, 4 }, 5); return BitConverter.ToString(p.ReadArray(4, 5));
        }, RepairScope);
        Check("PACKET-10-length-prefix-overflow", "REJECTED", () => Rejects(() => new ProbePacket(512).WriteStringWithLength(new string('x', 256), 4)), RepairScope);
        Check("PACKET-11-length-prefixed-CP1252", "4,caf\u00e9", () =>
        {
            var p = new ProbePacket(32); p.WriteStringWithLength("caf\u00e9", 4); return p.ReadByte(4) + "," + p.ReadString(4, 5);
        }, RepairScope);
        Check("ARENA-03-all-existing-boundaries", "1500,1500,2200,2200,2700,2700,3200,3200,4000", () =>
        {
            var a = new ArenaEngine(); return string.Join(",", new byte[] { 70, 89, 90, 109, 110, 119, 120, 129, 130 }.Select(a.GetStartupPoints));
        }, "Preserves upstream values and existing predicates; fixes ordering, not a new economy design.");

        foreach (string role in new[] { "server", "client", "internal" })
        {
            string copy = role;
            Check("TCP-" + role + "-disconnect-cleanup-synchronous-once", "True", () => FramingRegression.Run(copy, "disconnect"), RepairScope);
            Check("TCP-" + role + "-one-byte-header", "True", () => FramingRegression.Run(copy, "split"), RepairScope);
            Check("TCP-" + role + "-coalesced-frames", "True", () => FramingRegression.Run(copy, "coalesced"), RepairScope);
            foreach (string bad in new[] { "invalid-small", "invalid-large", "invalid-type" })
            {
                string mode = bad;
                Check("TCP-" + role + "-" + mode, "REJECTED", () => FramingRegression.Run(copy, mode), RepairScope);
            }
        }
        Check("DH-03-fragments-and-following-new-key-frame", "True", () => FramingRegression.Run("server", "exchange"), RepairScope);
        Check("VIP-01-five-different-city-maps", "1002,1011,1020,1000,1015", () =>
        {
            return string.Join(",", Enumerable.Range(1, 5).Select(i =>
            {
                RedMoon.GameServer.Structures.World.VipTeleportRoutes.TryGet((RedMoon.Core.Common.Enums.VIPTeleportLocations)i, out var route);
                return route.Map;
            }));
        }, "City entry points are from cq_map and checked against the archived walkable tiles; real teleport UI remains untested.");
        Check("VIP-02-unverified-subdestination-is-not-misrouted", "False", () =>
            RedMoon.GameServer.Structures.World.VipTeleportRoutes.TryGet(RedMoon.Core.Common.Enums.VIPTeleportLocations.WP_ALTAR, out _).ToString(), RepairScope);

        foreach (string password in new[] { "localfixture123", "MixedCase123", "AbCdEfGh12345678" })
        {
            string sample = password;
            Check("PASSWORD-roundtrip-" + sample.Length + "-" + (sample[0] == 'l' ? "lower" : "mixed"), sample, () =>
            {
                const string account = "ci_player"; const int seed = 123456;
                var packet = new ProbePacket(276); packet.WriteHeader(276, RedMoon.Core.Networking.Packets.PacketType.MSG_ACCOUNT1);
                packet.WriteString(account, 16, 4); packet.WriteString("RedMoon", 16, 260);
                byte[] bytes = new byte[16]; Encoding.ASCII.GetBytes(sample).CopyTo(bytes, 0);
                new NetDragonPasswordCipher(account).Encrypt(bytes); new RivestCipher5(seed).Encrypt(bytes); packet.WriteArray(bytes, 132);
                return new RedMoon.Core.Networking.Packets.MsgAccount(packet, seed).Password;
            }, "Synthetic fixed-field password test, including 16 non-NUL bytes; no actual owner's password.");
        }
        Check("RC5-managed-block-roundtrip", "True", () =>
        {
            byte[] data = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray(), expected = (byte[])data.Clone();
            var rc5 = new RivestCipher5(7654321); rc5.Encrypt(data); rc5.Decrypt(data); return data.SequenceEqual(expected).ToString();
        }, RepairScope);

        Check("INTERSERVER-01-auth-length-and-fields", "512,512,redmoon,0123456789abcdef,RedMoon,500", () =>
        {
            var sent = new RedMoon.Core.Networking.Packets.MsgLoginSvAuthentication("redmoon", "0123456789abcdef", "RedMoon", 500);
            byte[] bytes = sent; var read = new RedMoon.Core.Networking.Packets.MsgLoginSvAuthentication(bytes);
            return BitConverter.ToUInt16(bytes, 0) + "," + bytes.Length + "," + read.Username + "," + read.Password + "," + read.ServerName + "," + read.MaxOnlinePlayers;
        }, "Only the private RedMoon.AccountServer/RedMoon.GameServer link changes; its sockets have no client footer.");
        Check("INTERSERVER-02-information-length-name-port", "36,36,RedMoon,5816", () =>
        {
            var sent = new RedMoon.Core.Networking.Packets.MsgServerInformation(500, 0, "RedMoon", 5816);
            byte[] bytes = sent; var read = new RedMoon.Core.Networking.Packets.MsgServerInformation(bytes);
            return BitConverter.ToUInt16(bytes, 0) + "," + bytes.Length + "," + read.ServerName + "," + read.GamePort;
        }, RepairScope);
        Check("INTERSERVER-03-response-length", "24,24", () =>
        {
            byte[] bytes = new RedMoon.Core.Networking.Packets.LoginSvResponsePacket(RedMoon.Core.Common.Enums.LoginServerResponse.LOGIN_SUCCESSFUL);
            return BitConverter.ToUInt16(bytes, 0) + "," + bytes.Length;
        }, RepairScope);
        Check("TCP-listener-dispose-server", "True", () => FramingRegression.CloseListener(false), RepairScope);
        Check("TCP-listener-dispose-internal", "True", () => FramingRegression.CloseListener(true), RepairScope);

        Check("INTERSERVER-04-empty-MAC-is-initialized", "72,72,", () =>
        {
            byte[] bytes = new RedMoon.Core.Networking.Packets.MsgUsrLogin(1000001, 1234);
            var decoded = new RedMoon.Core.Networking.Packets.MsgUsrLogin(bytes);
            return BitConverter.ToUInt16(bytes, 0) + "," + bytes.Length + "," + decoded.MacAddress;
        }, RepairScope);
        Check("INTERSERVER-05-login-metadata-roundtrip", "1000001,1234,3,2,192.0.2.10,001122334455", () =>
        {
            byte[] bytes = new RedMoon.Core.Networking.Packets.MsgUsrLogin(1000001, 1234) { IpAddress="192.0.2.10", MacAddress="001122334455", VipLevel=3, Authority=2 };
            var d = new RedMoon.Core.Networking.Packets.MsgUsrLogin(bytes);
            return d.UserIdentity + "," + d.PacketHash + "," + d.VipLevel + "," + d.Authority + "," + d.IpAddress + "," + d.MacAddress;
        }, RepairScope);

        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "cast5-cfb64.json"))).RootElement;
        byte[] key = Convert.FromHexString(fixture.GetProperty("key_hex").GetString());
        byte[] iv = Convert.FromHexString(fixture.GetProperty("iv_hex").GetString());
        byte[] clear = Convert.FromHexString(fixture.GetProperty("plaintext_hex").GetString());
        byte[] encrypted = Convert.FromHexString(fixture.GetProperty("ciphertext_hex").GetString());
        Func<byte[], BlowfishCipher> cipher = k =>
        {
            BlowfishCipher.InitialKey = k;
            var c = new BlowfishCipher(); c.SetEncryptionIV(iv); c.SetDecryptionIV(iv); return c;
        };
        Check("CAST5-01-independent-OpenSSL-vector", Convert.ToHexString(encrypted), () => Convert.ToHexString(cipher(key).Encrypt(clear, clear.Length)), RepairScope);
        Check("CAST5-02-decrypt-independent-vector", Convert.ToHexString(clear), () => Convert.ToHexString(cipher(key).Decrypt(encrypted, encrypted.Length)), RepairScope);
        Check("CAST5-03-arbitrary-fragment-boundaries", Convert.ToHexString(encrypted), () =>
        {
            var c = cipher(key); int index = 0; var result = new byte[clear.Length];
            foreach (int size in new[] { 1, 7, 2, 16, 3, 38 })
            {
                byte[] part = clear.Skip(index).Take(size).ToArray(); byte[] bytes = c.Encrypt(part, part.Length);
                Buffer.BlockCopy(bytes, 0, result, index, bytes.Length); index += bytes.Length;
            }
            return Convert.ToHexString(result);
        }, RepairScope);
        Check("CAST5-04-offset-decryption", "True", () =>
        {
            var c = cipher(key); var target = Enumerable.Repeat((byte)0xEE, clear.Length + 8).ToArray();
            c.Decrypt(target, encrypted, encrypted.Length, 4);
            return (target.Take(4).All(x => x == 0xEE) && target.Skip(4).Take(clear.Length).SequenceEqual(clear) && target.Skip(clear.Length + 4).All(x => x == 0xEE)).ToString();
        }, RepairScope);
        Check("CAST5-05-DH-key-truncation-matches-OpenSSL", Convert.ToHexString(encrypted), () =>
        {
            byte[] longKey = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray(); return Convert.ToHexString(cipher(longKey).Encrypt(clear, clear.Length));
        }, "Upstream CAST_set_key truncates long DH secrets to 16 bytes; changing to Blowfish would not be equivalent.");
        Check("CAST5-06-invalid-output-does-not-advance-feedback", "REJECTED,True", () =>
        {
            var c = cipher(key); string rejects = Rejects(() => c.Decrypt(new byte[1], encrypted, encrypted.Length, 0));
            return rejects + "," + c.Decrypt(encrypted, encrypted.Length).SequenceEqual(clear);
        }, RepairScope);
        Check("CAST5-07-reset-key-and-IV", "True", () =>
        {
            var c = cipher(key); c.Encrypt(clear, 3); c.KeySchedule(key); c.SetEncryptionIV(iv);
            return c.Encrypt(clear, clear.Length).SequenceEqual(encrypted).ToString();
        }, RepairScope);
        Check("DH-01-independent-peers-agree", "True", () =>
        {
            var a = new DiffieHellmanKeyExchange(NetDragonDHKeyExchange.PRIMATIVE_ROOT, NetDragonDHKeyExchange.GENERATOR);
            var b = new DiffieHellmanKeyExchange(NetDragonDHKeyExchange.PRIMATIVE_ROOT, NetDragonDHKeyExchange.GENERATOR);
            string ak = a.GenerateRequest(), bk = b.GenerateRequest(); return a.GenerateResponse(bk).SequenceEqual(b.GenerateResponse(ak)).ToString();
        }, RepairScope);
        Check("DH-02-reject-trivial-public-key", "REJECTED", () =>
        {
            var a = new NetDragonDHKeyExchange(); return Rejects(() => a.GenerateResponse("01"));
        }, RepairScope);
    }
}
