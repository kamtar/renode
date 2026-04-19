//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.WebSockets.Misc;

using Newtonsoft.Json.Linq;

namespace Antmicro.Renode.WebSockets.Providers
{
    public class RenodeGuiApiProvider : IWebSocketAPIProvider
    {
        public bool Start(WebSocketAPISharedData sharedData)
        {
            SharedData = sharedData;
            SharedData.ClearEmulationEvent += ClearSubscriptions;
            SharedData.NewClientConnection += ClearSubscriptions;
            return true;
        }

        [WebSocketAPIAction("renode-gui/state", "1.6.0")]
        private WebSocketAPIResponse GetStateAction(string machine)
        {
            if(!TryResolveMachine(machine, false, out var resolvedMachine, out var error))
            {
                return WebSocketAPIUtils.CreateEmptyActionResponse(error);
            }

            var emulation = EmulationManager.Instance.CurrentEmulation;
            var timeSource = resolvedMachine != null ? resolvedMachine.LocalTimeSource : emulation.MasterTimeSource;
            var machines = emulation.Names.ToArray();
            var anyMachineRunning = emulation.Machines.Any(x => !x.IsPaused);

            return WebSocketAPIUtils.CreateActionResponse(new StateResponseDto
            {
                VirtualTimeSeconds = timeSource.ElapsedVirtualTime.TotalSeconds,
                HostTimeSeconds = timeSource.ElapsedHostTime.TotalSeconds,
                EmulationStarted = emulation.IsStarted,
                AnyMachineRunning = anyMachineRunning,
                Machines = machines,
                ActiveMachine = resolvedMachine != null ? emulation[resolvedMachine] : machines.FirstOrDefault()
            });
        }

        [WebSocketAPIAction("renode-gui/object/get", "1.6.0")]
        private WebSocketAPIResponse GetObjectValueAction(string target, string machine, string path, string memberPath)
        {
            if(!TryResolveTarget(target, machine, path, out var resolvedTarget, out var error))
            {
                return WebSocketAPIUtils.CreateEmptyActionResponse(error);
            }

            if(!TryReadMemberPath(resolvedTarget, memberPath, out var value, out error))
            {
                return WebSocketAPIUtils.CreateEmptyActionResponse(error);
            }

            return WebSocketAPIUtils.CreateActionResponse(NormalizeValue(value));
        }

        [WebSocketAPIAction("renode-gui/object/invoke", "1.6.0")]
        private WebSocketAPIResponse InvokeObjectAction(string target, string machine, string path, string method, JArray arguments)
        {
            if(!TryResolveTarget(target, machine, path, out var resolvedTarget, out var error))
            {
                return WebSocketAPIUtils.CreateEmptyActionResponse(error);
            }

            if(!TryInvoke(resolvedTarget, method, arguments, out var value, out error))
            {
                return WebSocketAPIUtils.CreateEmptyActionResponse(error);
            }

            return WebSocketAPIUtils.CreateActionResponse(NormalizeValue(value));
        }

        [WebSocketAPIAction("renode-gui/events/watch", "1.6.0")]
        private WebSocketAPIResponse WatchObjectEventAction(string target, string machine, string path, string alias, string eventName)
        {
            if(!TryResolveTarget(target, machine, path, out var resolvedTarget, out var error))
            {
                return WebSocketAPIUtils.CreateEmptyActionResponse(error);
            }

            var effectiveAlias = string.IsNullOrWhiteSpace(alias) ? path : alias;
            if(!TrySubscribe(resolvedTarget, machine, path, effectiveAlias, eventName, out error))
            {
                return WebSocketAPIUtils.CreateEmptyActionResponse(error);
            }

            return WebSocketAPIUtils.CreateActionResponse(new WatchEventResponseDto
            {
                Alias = effectiveAlias,
                EventName = string.IsNullOrWhiteSpace(eventName) ? DetectEventName(resolvedTarget) : eventName,
                Path = path,
                Machine = machine
            });
        }

        [WebSocketAPIAction("renode-gui/events/clear", "1.6.0")]
        private WebSocketAPIResponse ClearWatchedEventsAction()
        {
            ClearSubscriptions();
            return WebSocketAPIUtils.CreateEmptyActionResponse();
        }

        [WebSocketAPIEvent("renode-gui/object-event", "1.6.0")]
        public WebSocketAPIEventHandler ObjectEvent;

        private void ClearSubscriptions()
        {
            foreach(var subscription in subscriptions)
            {
                subscription.Dispose();
            }
            subscriptions.Clear();
        }

        private bool TrySubscribe(object target, string machine, string path, string alias, string eventName, out string error)
        {
            error = null;

            var effectiveEventName = string.IsNullOrWhiteSpace(eventName) ? DetectEventName(target) : eventName;
            if(string.IsNullOrWhiteSpace(effectiveEventName))
            {
                error = string.Format("Could not detect an event to watch on '{0}'", path ?? target.GetType().Name);
                return false;
            }

            var eventInfo = target.GetType().GetEvent(effectiveEventName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if(eventInfo == null)
            {
                error = string.Format("Event '{0}' was not found on type '{1}'", effectiveEventName, target.GetType().FullName);
                return false;
            }

            if(eventInfo.EventHandlerType != typeof(Action<string>))
            {
                error = string.Format("Event '{0}' on type '{1}' is not supported; expected Action<string>", effectiveEventName, target.GetType().FullName);
                return false;
            }

            Action<string> handler = payload => ObjectEvent.RaiseEvent(new ObjectEventDto
            {
                Alias = alias,
                Machine = machine,
                Path = path,
                Event = effectiveEventName,
                Payload = payload ?? string.Empty
            });

            eventInfo.AddEventHandler(target, handler);
            subscriptions.Add(new DelegateSubscription(target, eventInfo, handler));
            return true;
        }

        private static string DetectEventName(object target)
        {
            var type = target.GetType();
            foreach(var candidate in new [] { "EventReceived", "EventQueued" })
            {
                var eventInfo = type.GetEvent(candidate, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if(eventInfo != null && eventInfo.EventHandlerType == typeof(Action<string>))
                {
                    return eventInfo.Name;
                }
            }
            return null;
        }

        private bool TryResolveTarget(string target, string machineName, string path, out object resolvedTarget, out string error)
        {
            resolvedTarget = null;
            error = null;

            switch((target ?? string.Empty).Trim().ToLowerInvariant())
            {
            case "emulation":
                resolvedTarget = EmulationManager.Instance.CurrentEmulation;
                return true;

            case "machine":
                if(!TryResolveMachine(machineName, true, out var machine, out error))
                {
                    return false;
                }
                resolvedTarget = machine;
                return true;

            case "peripheral":
                if(!TryResolveMachine(machineName, true, out var peripheralMachine, out error))
                {
                    return false;
                }
                if(string.IsNullOrWhiteSpace(path))
                {
                    error = "A peripheral path is required";
                    return false;
                }
                if(!peripheralMachine.TryGetByName(path, out IPeripheral peripheral))
                {
                    error = string.Format("Peripheral '{0}' was not found", path);
                    return false;
                }
                resolvedTarget = peripheral;
                return true;

            default:
                error = string.Format("Unsupported target '{0}'", target);
                return false;
            }
        }

        private static bool TryResolveMachine(string machineName, bool required, out IMachine machine, out string error)
        {
            error = null;
            machine = null;

            var emulation = EmulationManager.Instance.CurrentEmulation;
            if(!string.IsNullOrWhiteSpace(machineName))
            {
                if(emulation.TryGetMachine(machineName, out machine))
                {
                    return true;
                }
                error = string.Format("Machine '{0}' was not found", machineName);
                return false;
            }

            var allMachines = emulation.Machines.ToArray();
            if(allMachines.Length == 1)
            {
                machine = allMachines[0];
                return true;
            }

            if(required)
            {
                error = allMachines.Length == 0 ? "No machines are available" : "Machine name is required when more than one machine exists";
                return false;
            }

            return true;
        }

        private static bool TryReadMemberPath(object root, string memberPath, out object value, out string error)
        {
            value = root;
            error = null;

            if(string.IsNullOrWhiteSpace(memberPath))
            {
                return true;
            }

            foreach(var segment in memberPath.Split('.'))
            {
                if(value == null)
                {
                    error = string.Format("Member path '{0}' reached null before '{1}'", memberPath, segment);
                    return false;
                }

                if(!TryReadMember(value, segment, out value))
                {
                    error = string.Format("Member '{0}' was not found on type '{1}'", segment, value.GetType().FullName);
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadMember(object instance, string memberName, out object value)
        {
            value = null;
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            var type = instance.GetType();

            var property = type.GetProperty(memberName, flags);
            if(property != null && property.GetIndexParameters().Length == 0)
            {
                value = property.GetValue(instance, null);
                return true;
            }

            var field = type.GetField(memberName, flags);
            if(field != null)
            {
                value = field.GetValue(instance);
                return true;
            }

            return false;
        }

        private static bool TryInvoke(object instance, string methodName, JArray arguments, out object value, out string error)
        {
            value = null;
            error = null;

            if(string.IsNullOrWhiteSpace(methodName))
            {
                error = "A method name is required";
                return false;
            }

            var methods = instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                .Where(x => string.Equals(x.Name, methodName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if(methods.Length == 0)
            {
                error = string.Format("Method '{0}' was not found on type '{1}'", methodName, instance.GetType().FullName);
                return false;
            }

            var argumentArray = arguments != null ? arguments.ToArray() : new JToken[0];
            foreach(var method in methods)
            {
                var parameters = method.GetParameters();
                if(parameters.Length != argumentArray.Length)
                {
                    continue;
                }

                try
                {
                    var convertedArguments = new object[parameters.Length];
                    for(var i = 0; i < parameters.Length; i++)
                    {
                        convertedArguments[i] = argumentArray[i].ToObject(parameters[i].ParameterType);
                    }

                    value = method.Invoke(instance, convertedArguments);
                    return true;
                }
                catch(TargetInvocationException e)
                {
                    error = e.InnerException != null ? e.InnerException.Message : e.Message;
                    return false;
                }
                catch(Exception)
                {
                    // Try the next overload.
                }
            }

            error = string.Format("No overload of '{0}' on type '{1}' matched {2} argument(s)", methodName, instance.GetType().FullName, argumentArray.Length);
            return false;
        }

        private static object NormalizeValue(object value)
        {
            return NormalizeValue(value, 0);
        }

        private static object NormalizeValue(object value, int depth)
        {
            if(value == null)
            {
                return null;
            }

            if(depth >= 4)
            {
                return value.ToString();
            }

            var type = value.GetType();
            if(type.IsEnum)
            {
                return value.ToString();
            }

            if(value is string || value is bool || value is byte || value is sbyte
                || value is short || value is ushort || value is int || value is uint
                || value is long || value is ulong || value is float || value is double
                || value is decimal)
            {
                return value;
            }

            if(value is TimeSpan)
            {
                return ((TimeSpan)value).TotalSeconds;
            }

            if(value is DateTime)
            {
                return ((DateTime)value).ToString("o", CultureInfo.InvariantCulture);
            }

            if(value is IDictionary)
            {
                var dictionary = new Dictionary<string, object>();
                foreach(DictionaryEntry entry in (IDictionary)value)
                {
                    dictionary[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = NormalizeValue(entry.Value, depth + 1);
                }
                return dictionary;
            }

            if(value is IEnumerable)
            {
                var list = new List<object>();
                foreach(var element in (IEnumerable)value)
                {
                    list.Add(NormalizeValue(element, depth + 1));
                }
                return list;
            }

            return value.ToString();
        }

        private WebSocketAPISharedData SharedData;
        private readonly List<DelegateSubscription> subscriptions = new List<DelegateSubscription>();

        private sealed class DelegateSubscription : IDisposable
        {
            public DelegateSubscription(object target, EventInfo eventInfo, Delegate handler)
            {
                this.target = target;
                this.eventInfo = eventInfo;
                this.handler = handler;
            }

            public void Dispose()
            {
                eventInfo.RemoveEventHandler(target, handler);
            }

            private readonly object target;
            private readonly EventInfo eventInfo;
            private readonly Delegate handler;
        }

        private sealed class StateResponseDto
        {
            public double VirtualTimeSeconds;
            public double HostTimeSeconds;
            public bool EmulationStarted;
            public bool AnyMachineRunning;
            public string[] Machines;
            public string ActiveMachine;
        }

        private sealed class WatchEventResponseDto
        {
            public string Alias;
            public string EventName;
            public string Path;
            public string Machine;
        }

        private sealed class ObjectEventDto
        {
            public string Alias;
            public string Machine;
            public string Path;
            public string Event;
            public string Payload;
        }
    }
}