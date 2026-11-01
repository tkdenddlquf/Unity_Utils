using System;
using System.Collections.Generic;

namespace Yang.Dialogue
{
    // Code generated into each consumer assembly registers direct, AOT-visible factories.
    public static class DialogueInstructionRegistry
    {
        private static readonly Dictionary<string, Func<RunnerCommand, object>> commands = new();
        private static readonly Dictionary<string, Func<RunnerCommand, object>> events = new();

        public static void RegisterCommand(string id, Func<RunnerCommand, object> factory) => commands[id] = factory;

        public static void RegisterEvent(string id, Func<RunnerCommand, object> factory) => events[id] = factory;

        internal static object Create(string id, bool isEvent, RunnerCommand command)
        {
            Dictionary<string, Func<RunnerCommand, object>> factories = isEvent ? events : commands;

            return factories.TryGetValue(id, out Func<RunnerCommand, object> factory) ? factory(command) : null;
        }
    }
}
