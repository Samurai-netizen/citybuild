#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Net;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Client.Presentation.Bootstrap
{
    /// <summary>How <see cref="StartupFlow"/> ended.</summary>
    public enum StartupResultKind
    {
        /// <summary>Logged in, the content is valid, and the city snapshot arrived.</summary>
        Connected = 0,

        /// <summary>A request failed (no connection, a timeout, a refusal, an answer that is not the contract's): trying again may work.</summary>
        Failed = 1,

        /// <summary>The server's content arrived but the engine's factory refused it: trying again will not help, the client shows a fatal error.</summary>
        InvalidContent = 2,
    }

    /// <summary>
    /// The result of <see cref="StartupFlow.RunAsync"/>: what the composition root shows now and what the next tasks start from. A connected
    /// result carries the content, the server clock's speed and the first snapshot's answer; the flow has put that snapshot in the city store.
    /// </summary>
    public sealed class StartupOutcome
    {
        /// <summary>Creates an outcome; use the static members.</summary>
        private StartupOutcome(StartupResultKind kind, GameContent? content, int timeScale, CitySnapshotDto? city, ApiError? error, IReadOnlyList<ContentError> contentErrors)
        {
            Kind = kind;
            Content = content;
            TimeScale = timeScale;
            City = city;
            Error = error;
            ContentErrors = contentErrors;
        }

        /// <summary>How the flow ended.</summary>
        public StartupResultKind Kind { get; }

        /// <summary>The validated content; set only when <see cref="Kind"/> is <see cref="StartupResultKind.Connected"/>.</summary>
        public GameContent? Content { get; }

        /// <summary>The speed of the server's clock, at least 1; 0 unless connected.</summary>
        public int TimeScale { get; }

        /// <summary>The first snapshot of the player's city; set only when connected.</summary>
        public CitySnapshotDto? City { get; }

        /// <summary>Why a request failed; set only when <see cref="Kind"/> is <see cref="StartupResultKind.Failed"/>.</summary>
        public ApiError? Error { get; }

        /// <summary>The factory's errors; not empty only when <see cref="Kind"/> is <see cref="StartupResultKind.InvalidContent"/>.</summary>
        public IReadOnlyList<ContentError> ContentErrors { get; }

        /// <summary>The flow reached the end.</summary>
        public static StartupOutcome Connected(GameContent content, int timeScale, CitySnapshotDto city)
        {
            return new StartupOutcome(StartupResultKind.Connected, content, timeScale, city, null, Array.Empty<ContentError>());
        }

        /// <summary>A request failed.</summary>
        public static StartupOutcome Failed(ApiError error)
        {
            return new StartupOutcome(StartupResultKind.Failed, null, 0, null, error, Array.Empty<ContentError>());
        }

        /// <summary>The content was refused by the factory; <paramref name="errors"/> is not empty.</summary>
        public static StartupOutcome InvalidContent(IReadOnlyList<ContentError> errors)
        {
            return new StartupOutcome(StartupResultKind.InvalidContent, null, 0, null, null, errors);
        }
    }
}
