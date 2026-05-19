using ComunaClick.Acl.Security;

var pwd = args.Length > 0 ? args[0] : "test123";
Console.WriteLine(PasswordHasher.Hash(pwd));
