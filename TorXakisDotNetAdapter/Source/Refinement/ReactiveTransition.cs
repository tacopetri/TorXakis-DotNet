﻿using System;
using System.Collections.Generic;
using System.Linq;
using TorXakisDotNetAdapter.Logging;

namespace TorXakisDotNetAdapter.Refinement
{
    /// <summary>
    /// A reactive <see cref="Transition"/>, responding to either:
    /// <para><see cref="ModelAction"/> inputs from the tester</para>
    /// <para><see cref="ISystemAction"/> events from the system</para>
    /// </summary>
    public sealed class ReactiveTransition : Transition
    {
        #region Base

        // TODO: Implement!

        #endregion
        #region Definitions

        // TODO: Implement!

        #endregion
        #region Variables & Properties

        /// <summary>
        /// The delegate signature of the <see cref="Guard"/> function.
        /// </summary>
        public delegate bool GuardDelegate(IAction action);
        /// <summary>
        /// The guard constraint: is this transition valid given the action?
        /// </summary>
        public GuardDelegate Guard { get; private set; }

        #endregion
        #region Create & Destroy

        /// <summary>
        /// Constructor, with parameters.
        /// </summary>
        public ReactiveTransition(Type action, State from, State to, GuardDelegate guard, UpdateDelegate update)
            : base(action, from, to, update)
        {
            Guard = guard ?? throw new ArgumentNullException(nameof(guard));
        }

        /// <summary><see cref="object.ToString"/></summary>
        public override string ToString()
        {
            return base.ToString();
        }

        #endregion
        #region Functionality

        // TODO: Implement!

        #endregion

    }
}
