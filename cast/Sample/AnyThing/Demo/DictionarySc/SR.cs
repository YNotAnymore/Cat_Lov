﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

namespace AnyThing.Demo.DictionarySc
{
    internal static partial class SR
    {
        private static readonly object _lock = new object();
        private static List<string>? _currentlyLoading;
        private static int _infinitelyRecursingCount;
        private static bool _resourceManagerInited;

        public const string Name = "System.Private.CoreLib";

        internal static string Format(string argument_InvalidTypeWithPointersNotSupported, Type targetType)
        {
            throw new NotImplementedException();
        }

        // Needed for debugger integration
        internal static string GetResourceString(string resourceKey)
        {
            return string.Empty;
            //return GetResourceString(resourceKey, null);
        }

        private static string InternalGetResourceString(string key)
        {
            if (key.Length == 0)
            {
                Debug.Fail("SR::GetResourceString with empty resourceKey.  Bug in caller, or weird recursive loading problem?");
                return key;
            }

            // We have a somewhat common potential for infinite
            // loops with mscorlib's ResourceManager.  If "potentially dangerous"
            // code throws an exception, we will get into an infinite loop
            // inside the ResourceManager and this "potentially dangerous" code.
            // Potentially dangerous code includes the IO package, CultureInfo,
            // parts of the loader, some parts of Reflection, Security (including
            // custom user-written permissions that may parse an XML file at
            // class load time), assembly load event handlers, etc.  Essentially,
            // this is not a bounded set of code, and we need to fix the problem.
            // Fortunately, this is limited to mscorlib's error lookups and is NOT
            // a general problem for all user code using the ResourceManager.

            // The solution is to make sure only one thread at a time can call
            // GetResourceString.  Also, since resource lookups can be
            // reentrant, if the same thread comes into GetResourceString
            // twice looking for the exact same resource name before
            // returning, we're going into an infinite loop and we should
            // return a bogus string.

            bool lockTaken = false;
            try
            {
                Monitor.Enter(_lock, ref lockTaken);

                // Are we recursively looking up the same resource?  Note - our backout code will set
                // the ResourceHelper's currentlyLoading stack to null if an exception occurs.
                if (_currentlyLoading != null && _currentlyLoading.Count > 0 && _currentlyLoading.LastIndexOf(key) != -1)
                {
                    // We can start infinitely recursing for one resource lookup,
                    // then during our failure reporting, start infinitely recursing again.
                    // avoid that.
                    if (_infinitelyRecursingCount > 0)
                    {
                        return key;
                    }
                    _infinitelyRecursingCount++;

                    // Note: our infrastructure for reporting this exception will again cause resource lookup.
                    // This is the most direct way of dealing with that problem.
                    string message = $"Infinite recursion during resource lookup within {Name}.  This may be a bug in {Name}, or potentially in certain extensibility points such as assembly resolve events or CultureInfo names.  Resource name: {key}";
                    Environment.FailFast(message);
                }

                _currentlyLoading ??= new List<string>();

                // Call class constructors preemptively, so that we cannot get into an infinite
                // loop constructing a TypeInitializationException.  If this were omitted,
                // we could get the Infinite recursion assert above by failing type initialization
                // between the Push and Pop calls below.
                if (!_resourceManagerInited)
                {
                    RuntimeHelpers.RunClassConstructor(typeof(ResourceManager).TypeHandle);
                    RuntimeHelpers.RunClassConstructor(typeof(ResourceReader).TypeHandle);
                    //RuntimeHelpers.RunClassConstructor(typeof(RuntimeResourceSet).TypeHandle);
                    RuntimeHelpers.RunClassConstructor(typeof(BinaryReader).TypeHandle);
                    _resourceManagerInited = true;
                }

                _currentlyLoading.Add(key); // Push

                string? s = ResourceManager.GetString(key, null);
                _currentlyLoading.RemoveAt(_currentlyLoading.Count - 1); // Pop

                Debug.Assert(s != null, "Managed resource string lookup failed.  Was your resource name misspelled?  Did you rebuild mscorlib after adding a resource to resources.txt?  Debug this w/ cordbg and bug whoever owns the code that called SR.GetResourceString.  Resource name was: \"" + key + "\"");
                return s ?? key;
            }
            catch
            {
                if (lockTaken)
                {
                    // Backout code - throw away potentially corrupt state
                    s_resourceManager = null;
                    _currentlyLoading = null;
                }
                throw;
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_lock);
                }
            }
        }

        internal static string Format(string arg_BogusIComparer, object comparer)
        {
            throw new NotImplementedException();
        }

        internal static string Format(string arg_WrongType, object key, Type targetType)
        {
            throw new NotImplementedException();
        }
    }
}
