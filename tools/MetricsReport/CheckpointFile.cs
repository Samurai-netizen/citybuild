using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CityBuilder.Simulation.Content;

namespace CityBuilder.MetricsReport;

/// <summary>
/// The checkpoint file, <c>server/analytics/checkpoints.json</c> (docs/plan/CR-0009-T2-PLAN.md §3.2): the named checkpoints and the
/// optional funnels. It is read strictly with <see cref="Utf8JsonReader"/>, which keeps positions, so every error names its line: an
/// unknown or repeated field, a missing <c>id</c>, <c>title</c> or <c>event</c>, a bad or duplicate id, an unknown event type, a
/// <c>detail</c> that is no content id, an <c>atLeast</c> below 1, and a funnel step that names no checkpoint or appears twice.
/// </summary>
/// <param name="Checkpoints">The checkpoints in file order (the order means nothing to the report).</param>
/// <param name="Funnels">The funnels in file order; empty when the file names none.</param>
public sealed partial record CheckpointFile(IReadOnlyList<Checkpoint> Checkpoints, IReadOnlyList<Funnel> Funnels)
{
    /// <summary>The name an error gives the file when the caller names none.</summary>
    public const string DefaultName = "checkpoints.json";

    /// <summary>Reads and checks a checkpoint file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="content">The game content; a <c>detail</c> must be one of its building, research or item ids.</param>
    /// <returns>The checkpoints and funnels.</returns>
    /// <exception cref="CheckpointFileException">The file can't be read or is broken.</exception>
    public static CheckpointFile Load(string path, GameContent content)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CheckpointFileException(path, 0, "the file can't be read: " + exception.Message, exception);
        }

        return Parse(json, content, path);
    }

    /// <summary>Checks the text of a checkpoint file.</summary>
    /// <param name="json">The file's text.</param>
    /// <param name="content">The game content; a <c>detail</c> must be one of its building, research or item ids.</param>
    /// <param name="fileName">The name errors give the file.</param>
    /// <returns>The checkpoints and funnels.</returns>
    /// <exception cref="CheckpointFileException">The text is broken.</exception>
    public static CheckpointFile Parse(string json, GameContent content, string fileName = DefaultName)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(content);
        return new Parser(json, content, fileName).ReadFile();
    }

    /// <summary>The form of a checkpoint id.</summary>
    /// <returns>The pattern <c>^[a-z0-9_]{1,48}$</c>.</returns>
    [GeneratedRegex("^[a-z0-9_]{1,48}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>A funnel step as read, with the line it is on, checked once every checkpoint is known.</summary>
    /// <param name="Id">The checkpoint id it names.</param>
    /// <param name="Line">Its line.</param>
    private sealed record Step(string Id, int Line);

    /// <summary>A funnel as read: its name and its steps with their lines.</summary>
    /// <param name="Name">The funnel's name.</param>
    /// <param name="Steps">Its steps in order.</param>
    private sealed record RawFunnel(string Name, IReadOnlyList<Step> Steps);

    /// <summary>One read of one file: the bytes, where each line starts, and the ids a <c>detail</c> may name.</summary>
    private sealed class Parser
    {
        /// <summary>The file as UTF-8, which the reader's positions count in.</summary>
        private readonly byte[] _bytes;

        /// <summary>The byte index where each line starts; entry <c>i</c> is line <c>i + 1</c>.</summary>
        private readonly int[] _lineStarts;

        /// <summary>Every building, research and item id of the content.</summary>
        private readonly HashSet<string> _contentIds;

        /// <summary>The name errors give the file.</summary>
        private readonly string _fileName;

        /// <summary>Prepares the read.</summary>
        /// <param name="json">The file's text.</param>
        /// <param name="content">The game content.</param>
        /// <param name="fileName">The name errors give the file.</param>
        public Parser(string json, GameContent content, string fileName)
        {
            _bytes = Encoding.UTF8.GetBytes(json);
            var starts = new List<int> { 0 };
            for (int index = 0; index < _bytes.Length; index++)
            {
                if (_bytes[index] == (byte)'\n')
                {
                    starts.Add(index + 1);
                }
            }

            _lineStarts = [.. starts];
            _contentIds = new HashSet<string>(StringComparer.Ordinal);
            _contentIds.UnionWith(content.Buildings.Select(building => building.Id.Value));
            _contentIds.UnionWith(content.Research.Select(research => research.Id.Value));
            _contentIds.UnionWith(content.Items.Select(item => item.Id.Value));
            _fileName = fileName;
        }

        /// <summary>Reads the whole file and checks the funnels against the checkpoints.</summary>
        /// <returns>The checkpoints and funnels.</returns>
        /// <exception cref="CheckpointFileException">The text is broken.</exception>
        public CheckpointFile ReadFile()
        {
            var checkpoints = new List<Checkpoint>();
            var funnels = new List<RawFunnel>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var reader = new Utf8JsonReader(_bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });
            try
            {
                reader.Read();
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw Fail(ref reader, "the file must hold one JSON object with \"checkpoints\" and \"funnels\".");
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                while (ReadProperty(ref reader, seen) is { } name)
                {
                    switch (name)
                    {
                        case "checkpoints":
                            ReadList(ref reader, name, (ref Utf8JsonReader entry) => checkpoints.Add(ReadCheckpoint(ref entry, ids)));
                            break;
                        case "funnels":
                            ReadList(ref reader, name, (ref Utf8JsonReader entry) => funnels.Add(ReadFunnel(ref entry)));
                            break;
                        default:
                            throw Fail(ref reader, $"Unknown field \"{name}\"; the file has \"checkpoints\" and \"funnels\".");
                    }
                }

                while (reader.Read())
                {
                    // A second value after the object makes the reader throw; reading to the end checks for it.
                }
            }
            catch (JsonException exception)
            {
                throw new CheckpointFileException(
                    _fileName, (int)(exception.LineNumber ?? 0) + 1, "the file is not valid JSON: " + exception.Message, exception);
            }

            return new CheckpointFile(checkpoints, funnels.Select(funnel => CheckFunnel(funnel, ids)).ToList());
        }

        /// <summary>A callback that reads one entry of a list, positioned on its first token.</summary>
        /// <param name="reader">The reader.</param>
        private delegate void EntryReader(ref Utf8JsonReader reader);

        /// <summary>Moves to the next property of an object; a repeated name is an error.</summary>
        /// <param name="reader">The reader, inside an object; left on the property's value.</param>
        /// <param name="seen">The names this object already had.</param>
        /// <returns>The property's name, or <see langword="null"/> at the end of the object.</returns>
        private string? ReadProperty(ref Utf8JsonReader reader, HashSet<string> seen)
        {
            reader.Read();
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return null;
            }

            string name = reader.GetString()!;
            if (!seen.Add(name))
            {
                throw Fail(ref reader, $"\"{name}\" appears twice in one object.");
            }

            reader.Read();
            return name;
        }

        /// <summary>Reads a list of objects and hands each one to a callback.</summary>
        /// <param name="reader">The reader, on the list's value.</param>
        /// <param name="name">The list's field name, for errors.</param>
        /// <param name="readEntry">Reads one entry, starting on its <c>{</c>.</param>
        private void ReadList(ref Utf8JsonReader reader, string name, EntryReader readEntry)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw Fail(ref reader, $"\"{name}\" must be a list.");
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw Fail(ref reader, $"each entry of \"{name}\" must be an object.");
                }

                readEntry(ref reader);
            }
        }

        /// <summary>Reads and checks one checkpoint.</summary>
        /// <param name="reader">The reader, on the checkpoint's <c>{</c>; left on its <c>}</c>.</param>
        /// <param name="ids">The ids read so far; the new one is added.</param>
        /// <returns>The checkpoint.</returns>
        private Checkpoint ReadCheckpoint(ref Utf8JsonReader reader, HashSet<string> ids)
        {
            int entryLine = Line(ref reader);
            string? id = null, title = null, eventType = null, detail = null, outcome = null;
            long? minValue = null;
            int atLeast = 1, idLine = entryLine;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (ReadProperty(ref reader, seen) is { } name)
            {
                switch (name)
                {
                    case "id":
                        idLine = Line(ref reader);
                        id = ReadText(ref reader, name);
                        break;
                    case "title":
                        title = ReadText(ref reader, name);
                        break;
                    case "event":
                        eventType = ReadText(ref reader, name);
                        if (!KnownEvents.IsKnown(eventType))
                        {
                            throw Fail(ref reader, $"Unknown event \"{eventType}\": it must be \"command.\" and a command type, one of the "
                                + "recorded names (research.completed, building.completed, session.start, player.created) or production.first.");
                        }

                        break;
                    case "detail":
                        detail = ReadText(ref reader, name);
                        if (!_contentIds.Contains(detail))
                        {
                            throw Fail(ref reader, $"The detail \"{detail}\" is no building, research or item id of the content.");
                        }

                        break;
                    case "outcome":
                        outcome = ReadText(ref reader, name);
                        break;
                    case "minValue":
                        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out long value))
                        {
                            throw Fail(ref reader, "\"minValue\" must be a whole number.");
                        }

                        minValue = value;
                        break;
                    case "atLeast":
                        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out atLeast) || atLeast < 1)
                        {
                            throw Fail(ref reader, "\"atLeast\" must be a whole number of at least 1.");
                        }

                        break;
                    default:
                        throw Fail(ref reader, $"Unknown field \"{name}\"; a checkpoint has id, title, event, detail, outcome, minValue and atLeast.");
                }
            }

            id = id ?? throw new CheckpointFileException(_fileName, entryLine, "\"id\" is missing.");
            title = title ?? throw new CheckpointFileException(_fileName, entryLine, "\"title\" is missing.");
            eventType = eventType ?? throw new CheckpointFileException(_fileName, entryLine, "\"event\" is missing.");
            if (!IdPattern().IsMatch(id))
            {
                throw new CheckpointFileException(_fileName, idLine, $"The id \"{id}\" must be 1 to 48 of a-z, 0-9 and _.");
            }

            if (!ids.Add(id))
            {
                throw new CheckpointFileException(_fileName, idLine, $"Duplicate id \"{id}\".");
            }

            return new Checkpoint(id, title, eventType, detail, outcome, minValue, atLeast);
        }

        /// <summary>Reads one funnel; its steps are checked later, when every checkpoint is known.</summary>
        /// <param name="reader">The reader, on the funnel's <c>{</c>; left on its <c>}</c>.</param>
        /// <returns>The funnel as read.</returns>
        private RawFunnel ReadFunnel(ref Utf8JsonReader reader)
        {
            int entryLine = Line(ref reader);
            string? funnelName = null;
            List<Step>? steps = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (ReadProperty(ref reader, seen) is { } name)
            {
                switch (name)
                {
                    case "name":
                        funnelName = ReadText(ref reader, name);
                        break;
                    case "steps":
                        if (reader.TokenType != JsonTokenType.StartArray)
                        {
                            throw Fail(ref reader, "\"steps\" must be a list of checkpoint ids.");
                        }

                        steps = [];
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            steps.Add(new Step(ReadText(ref reader, "steps"), Line(ref reader)));
                        }

                        break;
                    default:
                        throw Fail(ref reader, $"Unknown field \"{name}\"; a funnel has name and steps.");
                }
            }

            funnelName = funnelName ?? throw new CheckpointFileException(_fileName, entryLine, "\"name\" is missing.");
            steps = steps ?? throw new CheckpointFileException(_fileName, entryLine, "\"steps\" is missing.");
            if (steps.Count == 0)
            {
                throw new CheckpointFileException(_fileName, entryLine, $"The funnel \"{funnelName}\" has no steps.");
            }

            return new RawFunnel(funnelName, steps);
        }

        /// <summary>Checks a funnel's steps: each names a checkpoint, none twice.</summary>
        /// <param name="funnel">The funnel as read.</param>
        /// <param name="ids">Every checkpoint id of the file.</param>
        /// <returns>The funnel.</returns>
        private Funnel CheckFunnel(RawFunnel funnel, HashSet<string> ids)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (Step step in funnel.Steps)
            {
                if (!ids.Contains(step.Id))
                {
                    throw new CheckpointFileException(_fileName, step.Line, $"The funnel step \"{step.Id}\" names no checkpoint of the file.");
                }

                if (!used.Add(step.Id))
                {
                    throw new CheckpointFileException(_fileName, step.Line, $"The funnel \"{funnel.Name}\" names \"{step.Id}\" twice.");
                }
            }

            return new Funnel(funnel.Name, funnel.Steps.Select(step => step.Id).ToList());
        }

        /// <summary>Reads a text value that is not blank.</summary>
        /// <param name="reader">The reader, on the value.</param>
        /// <param name="name">The field's name, for errors.</param>
        /// <returns>The text.</returns>
        private string ReadText(ref Utf8JsonReader reader, string name)
        {
            if (reader.TokenType != JsonTokenType.String || reader.GetString() is not { } text || string.IsNullOrWhiteSpace(text))
            {
                throw Fail(ref reader, $"\"{name}\" must be a text that is not empty.");
            }

            return text;
        }

        /// <summary>The 1-based line of the reader's current token.</summary>
        /// <param name="reader">The reader.</param>
        /// <returns>The line.</returns>
        private int Line(ref Utf8JsonReader reader)
        {
            int found = Array.BinarySearch(_lineStarts, (int)reader.TokenStartIndex);
            return found >= 0 ? found + 1 : ~found;
        }

        /// <summary>An error at the reader's current token.</summary>
        /// <param name="reader">The reader.</param>
        /// <param name="problem">What is wrong.</param>
        /// <returns>The error, to throw.</returns>
        private CheckpointFileException Fail(ref Utf8JsonReader reader, string problem) => new(_fileName, Line(ref reader), problem);
    }
}
