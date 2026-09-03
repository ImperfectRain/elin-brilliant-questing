using System;
using BrilliantQuesting.Lab.Cli;

namespace BrilliantQuesting.Lab
{
    /// <summary>
    /// Entry point for the headless laboratory. Everything the command line does - discovery,
    /// dispatch, seeding, option parsing, exit status - lives in <see cref="LabCommandLine"/>, and
    /// the experiments themselves are registered in <see cref="LabCatalog.Default"/>.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- list
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- describe questline
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run questline --seed 15
    ///
    /// The forms the laboratory has always accepted still work: no arguments or a bare seed runs
    /// the theft laboratory, and flags such as --ambient are registered aliases.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            return LabCommandLine.Execute(args, LabCatalog.Default(), Console.Out, Console.Error);
        }
    }
}
