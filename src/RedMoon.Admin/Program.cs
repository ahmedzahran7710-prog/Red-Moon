using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using RedMoon.Core.Security;

if (args.Length != 1) return 2;
if (args[0] == "hash-password")
{
    string? password = Console.ReadLine();
    if (password == null || password.Length < 8 || password.Length > 16 || password.Any(c => !char.IsAsciiLetterOrDigit(c)))
    {
        Console.Error.WriteLine("Game passwords must be 8-16 English letters/digits for this legacy keyboard protocol; no symbols or spaces.");
        return 2;
    }
    Console.WriteLine(WhirlpoolHash.Hash(password));
    return 0;
}
if (args[0] != "probe-login" || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true") return 2;
// CI-only, loopback-only synthetic peer. Never runs a retail client or uses owner credentials.
var input = JsonDocument.Parse(Console.ReadLine() ?? "{}").RootElement;
var outcome = new Dictionary<string, object> { ["Authorized"] = false, ["RedirectVerified"] = false, ["RealClientExecuted"] = false };
int status = 1;
try
{
    string account = input.GetProperty("Account").GetString()!;
    string password = input.GetProperty("Password").GetString()!;
    int port = input.GetProperty("Port").GetInt32(), gamePort = input.GetProperty("GamePort").GetInt32();
    using var client = new TcpClient(); client.Connect("127.0.0.1", port);
    using NetworkStream stream = client.GetStream(); stream.ReadTimeout = 10000; stream.WriteTimeout = 10000;
    var incoming = new NetDragonAuthenticationCipher();
    var outgoing = new NetDragonAuthenticationCipher();
    byte[] Transform(byte[] bytes, NetDragonAuthenticationCipher cipher)
    {
        // Server direction T(x)=nibbleSwap(x)^T(0). The peer uses its inverse.
        byte[] masks = cipher.Encrypt(new byte[bytes.Length], bytes.Length);
        return bytes.Select((b, i) => { byte v = (byte)(b ^ masks[i]); return (byte)((v >> 4) | (v << 4)); }).ToArray();
    }
    byte[] Read(int count)
    {
        var bytes = new byte[count]; int offset = 0;
        while (offset < count) { int got = stream.Read(bytes, offset, count - offset); if (got == 0) throw new IOException("Peer closed during synthetic probe."); offset += got; }
        return Transform(bytes, incoming);
    }
    byte[] Frame()
    {
        byte[] h = Read(4); int count = BitConverter.ToUInt16(h, 0);
        if (count < 4 || count > 2048) throw new InvalidDataException("Invalid response frame length.");
        return h.Concat(Read(count - 4)).ToArray();
    }
    byte[] seedPacket = Frame();
    if (seedPacket.Length != 8 || BitConverter.ToUInt16(seedPacket, 2) != 1059) throw new InvalidDataException("Missing login seed.");
    byte[] request = new byte[276];
    Buffer.BlockCopy(BitConverter.GetBytes((ushort)276), 0, request, 0, 2);
    Buffer.BlockCopy(BitConverter.GetBytes((ushort)1060), 0, request, 2, 2);
    Encoding.ASCII.GetBytes(account).CopyTo(request, 4);
    Encoding.ASCII.GetBytes("RedMoon").CopyTo(request, 260);
    byte[] pw = new byte[16]; Encoding.ASCII.GetBytes(password).CopyTo(pw, 0);
    new NetDragonPasswordCipher(account).Encrypt(pw);
    new RivestCipher5(BitConverter.ToInt32(seedPacket, 4)).Encrypt(pw);
    pw.CopyTo(request, 132);
    byte[] wire = Transform(request, outgoing);
    stream.Write(wire, 0, 1); Thread.Sleep(30); stream.Write(wire, 1, wire.Length - 1);
    byte[] response = Frame();
    if (response.Length != 52 || BitConverter.ToUInt16(response, 2) != 1055)
    {
        outcome["RejectionCode"] = response.Length >= 12 ? BitConverter.ToUInt32(response, 8) : 0;
        throw new InvalidDataException("Synthetic account was not authorized.");
    }
    bool redirect = Encoding.ASCII.GetString(response, 20, 32).TrimEnd('\0') == "127.0.0.1" && BitConverter.ToInt32(response, 12) == gamePort;
    if (!redirect) throw new InvalidDataException("Configured login redirect was not respected.");
    outcome["Authorized"] = true; outcome["RedirectVerified"] = true;
    outcome["FragmentedLoginRequest"] = true; status = 0;
}
catch (Exception error)
{
    outcome["ErrorType"] = error.GetType().Name;
    outcome["Message"] = error is InvalidDataException || error is IOException ? error.Message : "Synthetic authentication failed; no credential values emitted.";
}
string json = JsonSerializer.Serialize(outcome, new JsonSerializerOptions { WriteIndented = true });
string? temp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
if (temp != null) { Directory.CreateDirectory(Path.Combine(temp, "fox-evidence")); File.WriteAllText(Path.Combine(temp, "fox-evidence", "auth-probe-results.json"), json); }
Console.WriteLine(json);
return status;
