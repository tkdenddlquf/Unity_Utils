using System;
using System.Collections.Generic;

namespace Yang.Dialogue
{
    // Code generated into each consumer assembly registers direct, AOT-visible factories.
    public static class DialogueInstructionRegistry
    {
        private static readonly Dictionary<string, Func<RunnerCommand, object>> commands = new();
        private static readonly Dictionary<string, Func<RunnerCommand, object>> events = new();

        public static void RegisterCommand(string id, Func<RunnerCommand, object> factory) => Register(commands, id, factory, "Command");

        public static void RegisterEvent(string id, Func<RunnerCommand, object> factory) => Register(events, id, factory, "Event");

        private static void Register(Dictionary<string, Func<RunnerCommand, object>> target, string id, Func<RunnerCommand, object> factory, string kind)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException($"{kind} ID cannot be empty.", nameof(id));

            if (factory == null) throw new ArgumentNullException(nameof(factory));

            if (target.TryGetValue(id, out Func<RunnerCommand, object> existing))
            {
                // Enter Play Mode without domain reload can register the same factory again.
                if (existing == factory) return;

                throw new InvalidOperationException($"Dialogue {kind} ID '{id}' is already registered by a different factory. Check instruction IDs across assemblies.");
            }

            target.Add(id, factory);
        }

        internal static object Create(string id, bool isEvent, RunnerCommand command)
        {
            Dictionary<string, Func<RunnerCommand, object>> factories = isEvent ? events : commands;

            return factories.TryGetValue(id, out Func<RunnerCommand, object> factory) ? factory(command) : null;
        }
    }
}
