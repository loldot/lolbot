// See https://aka.ms/new-console-template for more information

string command;
while ((command = Console.ReadLine()) != "")
{
    if (command == "uci") Uci();
    else if (command == "isready") IsReady();
    else if (command.StartsWith("go")) Go();
    else Unknown(command);
}

void Uci()
{
    Console.WriteLine("id name Lolbot 1.0 alpha");
    Console.WriteLine("id author loldot");

    Console.WriteLine("uciok");
}

void IsReady()
{
    Console.WriteLine("readyok");
}

void Go()
{
    Console.WriteLine("bestmove ");
}

void Unknown(string command)
{
    Console.WriteLine($"Unknown command: {command}");
}