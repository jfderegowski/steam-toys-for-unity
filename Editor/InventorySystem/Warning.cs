using System;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>A problem the inspector reports: the message, and optionally how to fix it, offered as a Fix button.</summary>
    public class Warning
    {
        public string Message => _message;
        public bool CanFix => _fix != null;

        private readonly string _message;
        private readonly Action _fix;

        /// <param name="message">The message.</param>
        /// <param name="fix">Makes the warning go away; null when it needs a person to decide.</param>
        public Warning(string message, Action fix = null)
        {
            _message = message;
            _fix = fix;
        }

        /// <summary>A copy of <paramref name="warning"/>, for a subclass that adds what it is about.</summary>
        protected Warning(Warning warning) : this(warning._message, warning._fix) { }

        /// <summary>Runs the fix, when there is one.</summary>
        internal virtual void Fix() => _fix?.Invoke();
    }
}
