using ServerCore.Security;

if (args.Length != 1 || args[0] != "hash-password")
{
    Console.Error.WriteLine("Internal local setup helper: password input is stdin, never an argument.");
    return 2;
}
string? password = Console.ReadLine();
if (password == null || password.Length < 8 || password.Length > 16 || password.Any(c => c < 33 || c > 126))
{
    Console.Error.WriteLine("Game passwords must be 8-16 printable ASCII characters without spaces for this legacy client.");
    return 2;
}
Console.WriteLine(WhirlpoolHash.Hash(password));
return 0;
