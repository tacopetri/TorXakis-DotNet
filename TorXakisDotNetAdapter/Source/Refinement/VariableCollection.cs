﻿using System;
using System.Collections.Generic;
using System.Linq;
using TorXakisDotNetAdapter.Logging;

namespace TorXakisDotNetAdapter.Refinement
{
    /// <summary>
    /// A collection of named variables, with specific types and values.
    /// </summary>
    public sealed class VariableCollection
    {
        #region Definitions

        /// <summary>
        /// The supported <see cref="Type"/> of variables.
        /// </summary>
        private static readonly HashSet<Type> SupportedTypes = new HashSet<Type>()
        {
            typeof(bool),
            typeof(int),
            typeof(string),
        };

        #endregion
        #region Variables & Properties

        /// <summary>
        /// The collection of variables, indexed by name.
        /// </summary>
        private readonly Dictionary<string, object> variables = new Dictionary<string, object>();

        #endregion
        #region Create & Destroy

        /// <summary>
        /// Constructor, with parameters.
        /// </summary>
        public VariableCollection()
        {

        }

        /// <summary><see cref="object.ToString"/></summary>
        public override string ToString()
        {
            return variables.Count == 0 ? "None" : string.Join(", ", variables.Select(x => x.Key + " (" + x.Value.GetType().Name + ", " + x.Value + ")").ToArray());
        }

        #endregion
        #region Functionality

        /// <summary>
        /// Type-safe value setter for a named variable.
        /// </summary>
        public void SetValue<T>(string name, T value)
        {
            if (!SupportedTypes.Contains(typeof(T)))
                throw new ArgumentException("Type not supported: " + typeof(T));

            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (variables.TryGetValue(name, out object existing))
            {
                if (existing.GetType() != value.GetType())
                    throw new ArgumentException("Cannot switch type! Name: " + name + " Old: " + existing + " (" + existing.GetType() + ")" + " New: " + value + " (" + value.GetType() + ")");
            }

            // All checks passed, assign the value!
            Log.Debug(this, "Setting variable! Name: " + name + " Type: " + value.GetType().Name + " Value: " + value);
            variables[name] = value;
        }

        /// <summary>
        /// Type-safe value getter for a named variable.
        /// </summary>
        public T GetValue<T>(string name)
        {
            if (!SupportedTypes.Contains(typeof(T)))
                throw new ArgumentException("Type not supported: " + typeof(T));

            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            if (!variables.TryGetValue(name, out object existing))
                throw new ArgumentException("Variable not set: " + name);

            if (existing.GetType() != typeof(T))
                throw new ArgumentException("Cannot convert type! Name: " + name + " Set: " + existing + " (" + existing.GetType() + ")" + " Get: " + typeof(T));

            // All checks passed, return the value!
            Log.Debug(this, "Getting variable! Name: " + name + " Type: " + existing.GetType().Name + " Value: " + existing);
            return (T)existing;
        }

        /// <summary>
        /// Type-safe value clearing for a named variable.
        /// </summary>
        public void ClearValue(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            if (!variables.TryGetValue(name, out object existing))
                throw new ArgumentException("Variable not set: " + name);

            // All checks passed, clear the value!
            Log.Debug(this, "Clearing variable! Name: " + name + " Type: " + existing.GetType().Name + " Value: " + existing);
            variables.Remove(name);
        }

        #endregion
    }
}
