using DbUp;
using System.Reflection;

var connectionString = args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("SqlConnectionString")
    ?? throw new InvalidOperationException("Connection string not provided.");

Console.WriteLine("Starting database migration...");

var upgrader = DeployChanges.To
    .SqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
    .WithTransactionPerScript()
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();

if (!result.Successful)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Migration failed: {result.Error}");
    Console.ResetColor();
    Environment.Exit(1);
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Migration completed successfully.");
Console.ResetColor();