﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TorXakisDotNetAdapter.Logging;

namespace TorXakisDotNetAdapter.Refinement
{
    /// <summary>
    /// The top-level class that manages all action refinement operations.
    /// </summary>
    public sealed class RefinementFramework
    {
        #region Definitions

        /// <summary>
        /// Force atomic refinements? This means that no other refinement may start when another one is still in progress.
        /// </summary>
        public static readonly bool AtomicRefinement = true;

        #endregion
        #region Variables & Properties

        /// <summary>
        /// A lock object to make this class thread-safe.
        /// </summary>
        private readonly object locker = new object();

        /// <summary>
        /// The managed <see cref="TorXakisConnector"/> instance.
        /// </summary>
        public TorXakisConnector Connector { get; private set; }

        /// <summary>
        /// The collection of contained <see cref="TransitionSystem"/> systems.
        /// </summary>
        public HashSet<TransitionSystem> Systems { get; private set; } = new HashSet<TransitionSystem>();

        /// <summary>
        /// The current <see cref="TransitionSystem"/> system.
        /// </summary>
        public TransitionSystem CurrentSystem { get; private set; }

        /// <summary>
        /// The <see cref="ModelAction"/> types that are contained somewhere in <see cref="ReactiveTransition"/> transitions,
        /// of the contained <see cref="TransitionSystem"/> systems. This allows pre-filtering of incoming model inputs.
        /// </summary>
        public HashSet<Type> ModelInputs { get; private set; } = new HashSet<Type>();

        /// <summary>
        /// The <see cref="ModelAction"/> types that are contained somewhere in <see cref="ProactiveTransition"/> transitions,
        /// of the contained <see cref="TransitionSystem"/> systems. This allows pre-filtering of outgoing model outputs.
        /// </summary>
        public HashSet<Type> ModelOutputs { get; private set; } = new HashSet<Type>();

        /// <summary>
        /// The <see cref="ISystemAction"/> types that are contained somewhere in <see cref="ProactiveTransition"/> transitions,
        /// of the contained <see cref="TransitionSystem"/> systems. This allows pre-filtering of outgoing system commands.
        /// </summary>
        public HashSet<Type> SystemCommands { get; private set; } = new HashSet<Type>();

        /// <summary>
        /// The <see cref="ISystemAction"/> types that are contained somewhere in <see cref="ReactiveTransition"/> transitions,
        /// of the contained <see cref="TransitionSystem"/> systems. This allows pre-filtering of incoming system events.
        /// </summary>
        public HashSet<Type> SystemEvents { get; private set; } = new HashSet<Type>();

        #endregion
        #region Create & Destroy

        /// <summary>
        /// Constructor, with parameters.
        /// </summary>
        public RefinementFramework(FileInfo model)
        {
            Connector = new TorXakisConnector(model);
            Connector.Started += Connector_Started;
            Connector.InputReceived += Connector_InputReceived;
        }

        /// <summary><see cref="object.ToString"/></summary>
        public override string ToString()
        {
            return GetType().Name
                + "\n" + nameof(Connector) + ": " + Connector
                + "\n" + nameof(Systems) + " (" + Systems.Count + "): " + string.Join(", ", Systems.Select(x => x.ModelAction.Name).ToArray())
                + "\n" + nameof(CurrentSystem) + ": " + CurrentSystem
                + "\n" + nameof(ModelInputs) + " (" + ModelInputs.Count + "): " + string.Join(", ", ModelInputs.Select(x => x.Name).ToArray())
                + "\n" + nameof(ModelOutputs) + " (" + ModelOutputs.Count + "): " + string.Join(", ", ModelOutputs.Select(x => x.Name).ToArray())
                + "\n" + nameof(SystemCommands) + " (" + SystemCommands.Count + "): " + string.Join(", ", SystemCommands.Select(x => x.Name).ToArray())
                + "\n" + nameof(SystemEvents) + " (" + SystemEvents.Count + "): " + string.Join(", ", SystemEvents.Select(x => x.Name).ToArray());
        }

        #endregion
        #region Connector

        /// <summary><see cref="TorXakisConnector.Start"/></summary>
        public void Start()
        {
            lock (locker)
            {
                Connector.Start();
            }
        }

        /// <summary><see cref="TorXakisConnector.Stop"/></summary>
        public void Stop()
        {
            lock (locker)
            {
                Connector.Stop();
            }
        }

        /// <summary><see cref="TorXakisConnector.Started"/></summary>
        public event Action Started;

        /// <summary><see cref="TorXakisConnector.Started"/></summary>
        private void Connector_Started()
        {
            lock (locker)
            {
                Started?.Invoke();
            }
        }

        /// <summary><see cref="TorXakisConnector.InputReceived"/></summary>
        private void Connector_InputReceived(TorXakisAction action)
        {
            if (action.Type == ActionType.Input && action.Channel == TorXakisModel.InputChannel)
            {
                ModelAction input = ModelAction.Deserialize(action.Data);
                HandleModelInput(input);
            }
        }

        #endregion
        #region Systems

        /// <summary>
        /// Adds the given <see cref="TransitionSystem"/> to <see cref="Systems"/>.
        /// </summary>
        public bool AddSystem(TransitionSystem system)
        {
            lock (locker)
            {
                bool success = Systems.Add(system);
                if (success) IndexSystems();
                return success;
            }
        }

        /// <summary>
        /// Removes the given <see cref="TransitionSystem"/> from <see cref="Systems"/>.
        /// </summary>
        public bool RemoveSystem(TransitionSystem system)
        {
            lock (locker)
            {
                bool success = Systems.Remove(system);
                if (success) IndexSystems();
                return success;
            }
        }

        /// <summary>
        /// Indexes the <see cref="ModelInputs"/> and <see cref="SystemEvents"/>,
        /// plus <see cref="ModelOutputs"/> and <see cref="SystemCommands"/>,
        /// whenever a <see cref="TransitionSystem"/> is added or removed.
        /// </summary>
        private void IndexSystems()
        {
            ModelInputs.Clear();
            ModelOutputs.Clear();
            SystemCommands.Clear();
            SystemEvents.Clear();

            foreach (TransitionSystem system in Systems)
            {
                foreach (Transition transition in system.Transitions)
                {
                    if (transition is ReactiveTransition reactive)
                    {
                        if (typeof(ModelAction).IsAssignableFrom(reactive.Action))
                            ModelInputs.Add(reactive.Action);
                        else if (typeof(ISystemAction).IsAssignableFrom(reactive.Action))
                            SystemEvents.Add(reactive.Action);
                    }
                    else if (transition is ProactiveTransition proactive)
                    {
                        if (typeof(ModelAction).IsAssignableFrom(proactive.Action))
                            ModelOutputs.Add(proactive.Action);
                        else if (typeof(ISystemAction).IsAssignableFrom(proactive.Action))
                            SystemCommands.Add(proactive.Action);
                    }
                }
            }
        }

        /// <summary>
        /// Checks the <see cref="inputs"/> and <see cref="events"/> queues.
        /// Determines if the <see cref="CurrentSystem"/> can be advanced.
        /// Determines if one of the <see cref="Systems"/> can be started.
        /// </summary>
        public void CheckSystems()
        {
            Log.Debug(this, nameof(CheckSystems) + " inputs: " + inputs.Count + " commands: " + events.Count + "\n" + this);

            bool transitioned = false;

            // If proactive transitions are possible, trigger them!
            if (!transitioned)
            {
                HashSet<Tuple<TransitionSystem, ProactiveTransition>> proactives = PossibleProactiveTransitions();
                if (proactives.Count > 0)
                {
                    Log.Debug(this, "Possible proactive transitions: " + string.Join(", ", proactives.Select(x => x.Item1.ModelAction.Name + ": " + x.Item2).ToArray()));
                    Tuple<TransitionSystem, ProactiveTransition> selected = proactives.Random();
                    Log.Debug(this, "Selected proactive transition: " + selected.Item1.ModelAction.Name + ": " + selected.Item2);
                    IAction generated = ExecuteProactiveTransition(selected);

                    if (generated is ModelAction modelOutput)
                        SendModelOutput(modelOutput);
                    else if (generated is ISystemAction systemCommand)
                        SendSystemCommand(systemCommand);

                    transitioned = true;
                }
            }

            // If reactive transitions are possible, due to system events, trigger them!
            if (!transitioned)
            {
                if (events.Count > 0)
                {
                    ISystemAction systemEvent = events.Dequeue();
                    Log.Debug(this, "Dequeueing system event: " + systemEvent);
                    HashSet<Tuple<TransitionSystem, ReactiveTransition>> reactives = PossibleReactiveTransitions(systemEvent);
                    if (reactives.Count > 0)
                    {
                        Log.Debug(this, "Possible reactive transitions: " + string.Join(", ", reactives.Select(x => x.Item1.ModelAction.Name + ": " + x.Item2).ToArray()));
                        Tuple<TransitionSystem, ReactiveTransition> selected = reactives.Random();
                        Log.Debug(this, "Selected reactive transition: " + selected.Item1.ModelAction.Name + ": " + selected.Item2);
                        ExecuteReactiveTransition(systemEvent, selected);

                        transitioned = true;
                    }
                    else
                    {
                        // Since all system events are being looped through this, not being able to handle one is not an error per se. (TPE)
                        Log.Warn(this, "No reactive transition possible for system event: " + systemEvent);
                    }
                }
            }

            // If reactive transitions are possible, due to model inputs, trigger them!
            if (!transitioned)
            {
                if (inputs.Count > 0)
                {
                    ModelAction modelInput = inputs.Dequeue();
                    Log.Debug(this, "Dequeueing model input: " + modelInput);
                    HashSet<Tuple<TransitionSystem, ReactiveTransition>> reactives = PossibleReactiveTransitions(modelInput);
                    if (reactives.Count > 0)
                    {
                        Log.Debug(this, "Possible reactive transitions: " + string.Join(", ", reactives.Select(x => x.Item1.ModelAction.Name + ": " + x.Item2).ToArray()));
                        Tuple<TransitionSystem, ReactiveTransition> selected = reactives.Random();
                        Log.Debug(this, "Selected reactive transition: " + selected.Item1.ModelAction.Name + ": " + selected.Item2);
                        ExecuteReactiveTransition(modelInput, selected);

                        transitioned = true;
                    }
                    else
                    {
                        Log.Error(this, "No reactive transition possible for model input: " + modelInput);
                        // Notify TorXakis immediately that a refinement error occurred: don't wait until quiescence is observed.
                        // This prevents more TorXakis inputs coming before an output is finally expected (which never comes).
                        SendModelOutput(new ErrorAction());
                    }
                }
            }

            // If a transition was taken, re-evaluate immediately!
            if (transitioned)
            {
                if (CurrentSystem.CurrentState == CurrentSystem.InitialState)
                {
                    Log.Debug(this, "System has looped: " + CurrentSystem);
                    CurrentSystem = null;
                }
                CheckSystems();
            }
            // If no transition was taken (due to ignored input or event), we still need to check the next input or event.
            else if (inputs.Count > 0 || events.Count > 0)
            {
                CheckSystems();
            }
        }

        #endregion
        #region Systems Aggregation

        /// <summary>
        /// Aggregates <see cref="TransitionSystem.PossibleReactiveTransitions"/>.
        /// <para>If <see cref="CurrentSystem"/> is NULL, parse all <see cref="Systems"/>.</para>
        /// <para>If <see cref="CurrentSystem"/> is NOT NULL, only parse that.</para>
        /// </summary>
        public HashSet<Tuple<TransitionSystem, ReactiveTransition>> PossibleReactiveTransitions(IAction action)
        {
            HashSet<Tuple<TransitionSystem, ReactiveTransition>> result = new HashSet<Tuple<TransitionSystem, ReactiveTransition>>();

            // If current system is set, only query that.
            if (AtomicRefinement && CurrentSystem != null)
            {
                foreach (ReactiveTransition transition in CurrentSystem.PossibleReactiveTransitions(action))
                    result.Add(new Tuple<TransitionSystem, ReactiveTransition>(CurrentSystem, transition));
            }
            // Otherwise, query all inactive systems.
            else
            {
                foreach (TransitionSystem system in Systems)
                    foreach (ReactiveTransition transition in system.PossibleReactiveTransitions(action))
                        result.Add(new Tuple<TransitionSystem, ReactiveTransition>(system, transition));
            }

            return result;
        }

        /// <summary>
        /// Aggregates <see cref="TransitionSystem.PossibleProactiveTransitions"/>.
        /// <para>If <see cref="CurrentSystem"/> is NULL, parse all <see cref="Systems"/>.</para>
        /// <para>If <see cref="CurrentSystem"/> is NOT NULL, only parse that.</para>
        /// </summary>
        public HashSet<Tuple<TransitionSystem, ProactiveTransition>> PossibleProactiveTransitions()
        {
            HashSet<Tuple<TransitionSystem, ProactiveTransition>> result = new HashSet<Tuple<TransitionSystem, ProactiveTransition>>();

            // If current system is set, only query that.
            if (AtomicRefinement && CurrentSystem != null)
            {
                foreach (ProactiveTransition transition in CurrentSystem.PossibleProactiveTransitions())
                    result.Add(new Tuple<TransitionSystem, ProactiveTransition>(CurrentSystem, transition));
            }
            // Otherwise, query all inactive systems.
            else
            {
                foreach (TransitionSystem system in Systems)
                    foreach (ProactiveTransition transition in system.PossibleProactiveTransitions())
                        result.Add(new Tuple<TransitionSystem, ProactiveTransition>(system, transition));
            }

            return result;
        }

        /// <summary>
        /// Executes the given <see cref="ReactiveTransition"/> transition,
        /// assuming it is contained in <see cref="PossibleReactiveTransitions"/>.
        /// </summary>
        public void ExecuteReactiveTransition(IAction action, Tuple<TransitionSystem, ReactiveTransition> transition)
        {
            if (!PossibleReactiveTransitions(action).Contains(transition))
                throw new ArgumentException("Transition not possible: " + transition.Item1 + ": " + transition.Item2);

            if (CurrentSystem == null || CurrentSystem == transition.Item1)
            {
                CurrentSystem = transition.Item1;
                transition.Item1.ExecuteReactiveTransition(action, transition.Item2);
            }
            else
                throw new ArgumentException("System cannot be activated: " + transition.Item1);
        }

        /// <summary>
        /// Executes the given <see cref="ProactiveTransition"/> transition,
        /// assuming it is contained in <see cref="PossibleProactiveTransitions"/>.
        /// Returns the generated <see cref="ModelAction"/> output or <see cref="ISystemAction"/> command.
        /// </summary>
        public IAction ExecuteProactiveTransition(Tuple<TransitionSystem, ProactiveTransition> transition)
        {
            if (!PossibleProactiveTransitions().Contains(transition))
                throw new ArgumentException("Transition not possible: " + transition.Item1 + ": " + transition.Item2);

            if (CurrentSystem == null || CurrentSystem == transition.Item1)
            {
                CurrentSystem = transition.Item1;
                IAction generated = transition.Item1.ExecuteProactiveTransition(transition.Item2);
                return generated;
            }
            else
                throw new ArgumentException("System cannot be activated: " + transition.Item1);
        }

        #endregion
        #region Inputs & Outputs

        /// <summary>
        /// The queue of waiting <see cref="ModelAction"/> inputs.
        /// </summary>
        private readonly Queue<ModelAction> inputs = new Queue<ModelAction>();

        /// <summary>
        /// Handles the given <see cref="ModelAction"/> input.
        /// </summary>
        public void HandleModelInput(ModelAction modelInput)
        {
            lock (locker)
            {
                // Pre-filter incompatible types.
                if (modelInput is null) throw new ArgumentNullException(nameof(modelInput));
                if (!ModelInputs.Contains(modelInput.GetType())) return;

                Log.Debug(this, nameof(HandleModelInput) + ": " + modelInput);
                inputs.Enqueue(modelInput);

                // This must now be called manually from the thread that should perform the operations.
                //CheckSystems();
            }
        }

        /// <summary>
        /// Sends the given <see cref="ModelAction"/> output.
        /// </summary>
        public void SendModelOutput(ModelAction modelOutput)
        {
            lock (locker)
            {
                // Pre-filter incompatible types.
                if (modelOutput is null) throw new ArgumentNullException(nameof(modelOutput));
                if (!ModelOutputs.Contains(modelOutput.GetType())) return;

                Log.Debug(this, nameof(SendModelOutput) + ": " + modelOutput);
                string serialized = modelOutput.Serialize();
                TorXakisAction output = TorXakisAction.FromOutput(TorXakisModel.OutputChannel, serialized);
                Connector.SendOutput(output);
            }
        }

        /// <summary>
        /// The queue of waiting <see cref="ISystemAction"/> events.
        /// </summary>
        private readonly Queue<ISystemAction> events = new Queue<ISystemAction>();


        /// <summary>
        /// Handles the given <see cref="ISystemAction"/> event.
        /// </summary>
        public void HandleSystemEvent(ISystemAction systemEvent)
        {
            lock (locker)
            {
                // Pre-filter incompatible types.
                if (systemEvent is null) throw new ArgumentNullException(nameof(systemEvent));
                if (!SystemEvents.Contains(systemEvent.GetType())) return;

                Log.Debug(this, nameof(HandleSystemEvent) + ": " + systemEvent);
                events.Enqueue(systemEvent);

                // This must now be called manually from the thread that should perform the operations.
                //CheckSystems();
            }
        }

        /// <summary>
        /// Signals that the SUT should execute the given <see cref="ISystemAction"/> command.
        /// </summary>
        public event Action<ISystemAction> ExecuteSystemCommand;

        /// <summary>
        /// Sends the given <see cref="ISystemAction"/> command.
        /// </summary>
        public void SendSystemCommand(ISystemAction systemCommand)
        {
            lock (locker)
            {
                // Pre-filter incompatible types.
                if (systemCommand is null) throw new ArgumentNullException(nameof(systemCommand));
                if (!SystemCommands.Contains(systemCommand.GetType())) return;

                Log.Debug(this, nameof(SendSystemCommand) + ": " + systemCommand);
                ExecuteSystemCommand?.Invoke(systemCommand);
            }
        }

        #endregion
    }
}
