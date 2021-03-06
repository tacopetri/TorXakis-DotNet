﻿using System;
using System.Collections.Generic;
using System.Linq;
using TorXakisDotNetAdapter.Logging;

namespace TorXakisDotNetAdapter.Refinement
{
    /// <summary>
    /// An Input Output Symbolic Transition System (IOSTS).
    /// <para>Consists of <see cref="State"/> states and <see cref="Transition"/> transitions.</para>
    /// </summary>
    public sealed class TransitionSystem
    {
        #region Variables & Properties

        /// <summary>
        /// The <see cref="Type"/> of the <see cref="Refinement.ModelAction"/> that is being refined via this system.
        /// </summary>
        public Type ModelAction { get; private set; }

        /// <summary>
        /// The collection of contained <see cref="State"/> states.
        /// </summary>
        public HashSet<State> States { get; private set; }

        /// <summary>
        /// The initial <see cref="State"/> state.
        /// </summary>
        public State InitialState { get; private set; }

        /// <summary>
        /// The current <see cref="State"/> state.
        /// </summary>
        public State CurrentState { get; private set; }

        /// <summary>
        /// The collection of contained <see cref="Transition"/> transitions.
        /// </summary>
        public HashSet<Transition> Transitions { get; private set; }

        /// <summary>
        /// The contained <see cref="VariableCollection"/>, holding all variables.
        /// </summary>
        public VariableCollection Variables { get; private set; } = new VariableCollection();

        #endregion
        #region Create & Destroy

        /// <summary>
        /// Constructor, with parameters.
        /// </summary>
        public TransitionSystem(HashSet<State> states, State initialState, HashSet<Transition> transitions)
        {
            // Sanity checks.
            if (states == null) throw new ArgumentNullException(nameof(states));
            if (initialState == null) throw new ArgumentNullException(nameof(initialState));
            if (!states.Contains(initialState)) throw new ArgumentException(nameof(initialState) + ": " + initialState);
            if (transitions == null) throw new ArgumentNullException(nameof(transitions));
            if (transitions.Any(x => !states.Contains(x.From) || !states.Contains(x.To))) throw new ArgumentException(nameof(transitions) + ": " + transitions);

            List<Type> modelActions = transitions.Where(x => typeof(ModelAction).IsAssignableFrom(x.Action)).Select(x => x.Action).ToList();
            if (modelActions.Count != 1) Log.Warn(this, "Invalid number of model actions: " + modelActions.Count);
            ModelAction = modelActions[0];
            States = states;
            InitialState = initialState;
            CurrentState = initialState;
            Transitions = transitions;
        }

        /// <summary><see cref="object.ToString"/></summary>
        public override string ToString()
        {
            return ModelAction.Name + " (" + GetType().Name + ")"
                + "\n\t" + nameof(States) + ": " + string.Join(", ", States.Select(x => x.ToString()).ToArray())
                + "\n\t" + nameof(InitialState) + ": " + InitialState
                + "\n\t" + nameof(CurrentState) + ": " + CurrentState
                + "\n\t" + nameof(Transitions) + ": " + string.Join(", ", Transitions.Select(x => x.ToString()).ToArray())
                + "\n\t" + nameof(Variables) + ": " + Variables;
        }

        #endregion
        #region Inputs & Outputs

        /// <summary>
        /// Returns the possible <see cref="ReactiveTransition"/> transitions,
        /// for the <see cref="CurrentState"/>,
        /// given the <see cref="ModelAction"/> input or <see cref="ISystemAction"/> event.
        /// </summary>
        public HashSet<ReactiveTransition> PossibleReactiveTransitions(IAction action)
        {
            HashSet<ReactiveTransition> result = new HashSet<ReactiveTransition>();
            foreach (ReactiveTransition transition in Transitions.Where(x => x is ReactiveTransition))
            {
                // Transition must come from the current state.
                if (transition.From != CurrentState) continue;
                // Transition must have the exact type of the given action.
                if (transition.Action != action.GetType()) continue;
                // Transition guard function must evaluate to true.
                if (!transition.ReactiveGuard(Variables, action)) continue;

                // All checks passed!
                result.Add(transition);
            }
            return result;
        }

        /// <summary>
        /// Returns the possible <see cref="ProactiveTransition"/> transitions,
        /// for the <see cref="CurrentState"/>.
        /// </summary>
        public HashSet<ProactiveTransition> PossibleProactiveTransitions()
        {
            HashSet<ProactiveTransition> result = new HashSet<ProactiveTransition>();
            foreach (ProactiveTransition transition in Transitions.Where(x => x is ProactiveTransition))
            {
                // Transition must come from the current state.
                if (transition.From != CurrentState) continue;
                // Transition guard function must evaluate to true.
                if (!transition.ProactiveGuard(Variables)) continue;

                // All checks passed!
                result.Add(transition);
            }
            return result;
        }

        /// <summary>
        /// Executes the given <see cref="ReactiveTransition"/> transition,
        /// assuming it is contained in <see cref="PossibleReactiveTransitions"/>.
        /// </summary>
        public void ExecuteReactiveTransition(IAction action, ReactiveTransition transition)
        {
            if (!PossibleReactiveTransitions(action).Contains(transition))
                throw new ArgumentException("Transition not possible: " + transition);

            // Execute the update function.
            Log.Debug(this, "Calling update function: " + transition);
            transition.UpdateVariables(Variables, action);
            // Transition to the new state.
            Log.Debug(this, "Transitioning to new state: " + transition.To);
            CurrentState = transition.To;
        }

        /// <summary>
        /// Executes the given <see cref="ProactiveTransition"/> transition,
        /// assuming it is contained in <see cref="PossibleProactiveTransitions"/>.
        /// Returns the generated <see cref="ModelAction"/> output or <see cref="ISystemAction"/> command.
        /// </summary>
        public IAction ExecuteProactiveTransition(ProactiveTransition transition)
        {
            if (!PossibleProactiveTransitions().Contains(transition))
                throw new ArgumentException("Transition not possible: " + transition);

            // Generate the action.
            Log.Debug(this, "Calling generate function: " + transition);
            IAction action = transition.GenerateAction(Variables);
            // Execute the update function.
            Log.Debug(this, "Calling update function: " + transition);
            transition.UpdateVariables(Variables, action);
            // Transition to the new state.
            Log.Debug(this, "Transitioning to new state: " + transition.To);
            CurrentState = transition.To;

            return action;
        }

        #endregion
    }
}
