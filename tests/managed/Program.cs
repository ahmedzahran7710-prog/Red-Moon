// Red Moon's independent diagnostics; upstream C# files are never patched.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using MsgServer.Structures.Qualifier;
using ServerCore.Common.Enums;
using ServerCore.Networking.Packets;

internal static class Program
{
    private sealed class ProbePacket : PacketStructure
    {
        public ProbePacket(int size) : base(size) { }
    }

    private sealed class Result
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public string Expected { get; set; }
        public string Observed { get; set; }
        public string Scope { get; set; }
    }

    private static readonly List<Result> Results = new List<Result>();

    private static void Check(string id, string expected, Func<string> observe, string scope)
    {
        var result = new Result { Id = id, Expected = expected, Scope = scope };
        try
        {
            result.Observed = observe();
            result.Status = result.Observed == expected ? "PASS" : "FAIL";
        }
        catch (Exception ex)
        {
            result.Status = "FAIL";
            result.Observed = ex.GetType().FullName + ": " + ex.Message;
        }
        Results.Add(result);
        Console.WriteLine(result.Status + " " + id + ": " + result.Observed);
    }

    private static int Main(string[] args)
    {
        const string packetScope = "Managed packet bytes only; no socket, native crypto, login server, or client is started.";
        Check("AUTH-01-success-length", "52", () =>
        {
            byte[] bytes = new MsgConnectEx(1000001, 0x12345678, "192.0.2.10", 5816);
            return bytes.Length.ToString();
        }, packetScope);
        Check("AUTH-02-success-header", "52,1055", () =>
        {
            byte[] bytes = new MsgConnectEx(1000001, 0x12345678, "192.0.2.10", 5816);
            return BitConverter.ToUInt16(bytes, 0) + "," + BitConverter.ToUInt16(bytes, 2);
        }, packetScope);
        Check("AUTH-03-field-offsets", "1000001,305419896,5816,0", () =>
        {
            byte[] bytes = new MsgConnectEx(1000001, 0x12345678, "192.0.2.10", 5816);
            return BitConverter.ToUInt32(bytes, 4) + "," + BitConverter.ToUInt32(bytes, 8)
                + "," + BitConverter.ToInt32(bytes, 12) + "," + BitConverter.ToUInt32(bytes, 16);
        }, packetScope);
        Check("AUTH-04-IP-at-offset-20", "192.0.2.10", () =>
        {
            byte[] bytes = new MsgConnectEx(1000001, 0x12345678, "192.0.2.10", 5816);
            return Encoding.ASCII.GetString(bytes, 20, 32).TrimEnd('\0');
        }, packetScope);
        Check("AUTH-05-rejection", "12,12,1055,1", () =>
        {
            byte[] bytes = new MsgConnectEx((RejectionType)1);
            return bytes.Length + "," + BitConverter.ToUInt16(bytes, 0) + ","
                + BitConverter.ToUInt16(bytes, 2) + "," + BitConverter.ToUInt32(bytes, 8);
        }, packetScope);
        Check("PACKET-01-short-field-control", "A1B2C3D4", () =>
        {
            var packet = new ProbePacket(32);
            packet.WriteUInt(0xA1B2C3D4, 8);
            packet.WriteString("ABC", 4, 4);
            byte[] bytes = packet;
            return BitConverter.ToUInt32(bytes, 8).ToString("X8");
        }, packetScope);
        Check("PACKET-02-field-boundary", "A1B2C3D4", () =>
        {
            var packet = new ProbePacket(32);
            packet.WriteUInt(0xA1B2C3D4, 8);
            packet.WriteString("ABCDEFGH", 4, 4);
            byte[] bytes = packet;
            return BitConverter.ToUInt32(bytes, 8).ToString("X8");
        }, "Contract probe: length is documented as maximum field length. Synthetic bytes only; checks adjacent-field corruption, not remote exploitability.");
        Check("ARENA-01-all-configured-tiers-reachable", "1500,2200,2700,3200,4000", () =>
        {
            var arena = new ArenaEngine(); // Does not call Create(), access DB, or start timers.
            return string.Join(",", Enumerable.Range(0, 256)
                .Select(level => arena.GetStartupPoints((byte)level)).Distinct().OrderBy(points => points));
        }, "Enumerates the real method over all possible byte levels. Unreachable configured tiers are a logic defect; intended new balancing/ranges are NOT specified by this test.");
        Check("ARENA-02-observed-boundary-control", "3200,4000", () =>
        {
            var arena = new ArenaEngine();
            return arena.GetStartupPoints(129) + "," + arena.GetStartupPoints(130);
        }, "Characterization of original boundary only; not approval of those reward values.");
        Check("ENCODING-01-fresh-process-ASCII-read", "192.0.2.10", () =>
        {
            return new MsgConnectEx(1000001, 0x12345678, "192.0.2.10", 5816).IPAddress;
        }, "Fresh .NET process, no explicit code-page registration or server/database startup. Failure here is conditional; dependency initialization during real startup remains untested.");

        var report = new
        {
            SchemaVersion = 1,
            SourceCommit = "70f3560465ba73cef0250109af7cf167b508516f",
            Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            Executed = Results.Count,
            Passed = Results.Count(result => result.Status == "PASS"),
            Failed = Results.Count(result => result.Status == "FAIL"),
            GameServerStarted = false,
            NativeCryptoExecuted = false,
            Results
        };
        if (args.Length == 2 && args[0] == "--report")
        {
            var fullPath = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        Console.WriteLine("RESULT: " + report.Passed + " passed, " + report.Failed + " failed. Upstream unchanged.");
        return report.Failed == 0 ? 0 : 1; // Baseline failures stay RED, never disguised as a pass.
    }
}
