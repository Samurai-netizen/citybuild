// The DocTools command line (docs/modules/DocTools.md): "generate" writes docs/reference/generated/, "check" regenerates into a temporary
// folder and fails with a summary when the committed pages differ (core-instructions/17-documentation-system.md §3).

using CityBuilder.DocTools;

return CommandLine.Run(args, Console.Out, Console.Error);
