using UnityEngine;
using UnityEditor;

namespace DungeonPainter.Editor
{
    /// <summary>
    /// Handles keyboard shortcuts for the Dungeon Painter tool
    /// </summary>
    public static class KeyboardShortcuts
    {
        public enum ShortcutAction
        {
            None,
            PaintRoom,
            PlaceNode,
            ConnectNodes,
            SelectMove,
            Delete,
            Undo,
            Redo,
            Copy,
            Paste,
            PanMode,
            CenterView
        }

        /// <summary>
        /// Processes keyboard input and returns the action to perform
        /// </summary>
        public static ShortcutAction ProcessInput(Event e)
        {
            if (e.type != EventType.KeyDown)
                return ShortcutAction.None;

            // Check modifier keys
            bool ctrl = e.control || e.command;
            bool shift = e.shift;
            bool alt = e.alt;

            // Undo/Redo (highest priority)
            if (ctrl && !shift && e.keyCode == KeyCode.Z)
            {
                e.Use();
                return ShortcutAction.Undo;
            }
            
            if (ctrl && shift && e.keyCode == KeyCode.Z)
            {
                e.Use();
                return ShortcutAction.Redo;
            }

            if (ctrl && e.keyCode == KeyCode.Y)
            {
                e.Use();
                return ShortcutAction.Redo;
            }

            // Copy/Paste
            if (ctrl && e.keyCode == KeyCode.C)
            {
                e.Use();
                return ShortcutAction.Copy;
            }

            if (ctrl && e.keyCode == KeyCode.V)
            {
                e.Use();
                return ShortcutAction.Paste;
            }

            // Pan mode (Space - hold to pan temporarily)
            if (e.keyCode == KeyCode.Space && !ctrl && !shift && !alt)
            {
                return ShortcutAction.PanMode;
            }

            // Tool mode shortcuts (only if no modifiers)
            if (!ctrl && !shift && !alt)
            {
                switch (e.keyCode)
                {
                    case KeyCode.P:
                        e.Use();
                        return ShortcutAction.PaintRoom;
                    
                    case KeyCode.N:
                        e.Use();
                        return ShortcutAction.PlaceNode;
                    
                    case KeyCode.C:
                        e.Use();
                        return ShortcutAction.ConnectNodes;
                    
                    case KeyCode.S:
                        e.Use();
                        return ShortcutAction.SelectMove;
                    
                    case KeyCode.D:
                        e.Use();
                        return ShortcutAction.Delete;
                    
                    case KeyCode.F:
                        e.Use();
                        return ShortcutAction.CenterView;
                }
            }

            return ShortcutAction.None;
        }

        /// <summary>
        /// Gets a tooltip string describing all shortcuts
        /// </summary>
        public static string GetShortcutHelpText()
        {
            return @"KEYBOARD SHORTCUTS:

TOOLS:
  P - Paint Room
  N - Place Node
  C - Connect Nodes
  S - Select/Move
  D - Delete

EDITING:
  Ctrl+Z - Undo
  Ctrl+Shift+Z / Ctrl+Y - Redo
  Ctrl+C - Copy selected room
  Ctrl+V - Paste room
  
NAVIGATION:
  Space (hold) - Pan mode
  Mouse Wheel - Zoom
  F - Center view
  Alt+Drag - Pan";
        }
    }
}
