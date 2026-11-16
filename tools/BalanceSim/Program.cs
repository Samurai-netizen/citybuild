// The balance simulator's entry point (docs/modules/BalanceSim.md; docs/how-to/run-balance-simulator.md): plays the content with simple
// strategies and check-in patterns and writes a markdown summary and a CSV timeline per run.

using CityBuilder.BalanceSim;

return CommandLine.Run(args, Console.Out, Console.Error);
