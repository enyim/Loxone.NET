// ----------------------------------------------------------------------
// <copyright file="TextState.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client.Controls
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class Command
    {
        private readonly Uuid _uuid;

        public Uuid Uuid => _uuid;

        private readonly string _command;

        public string Cmd => _command;

        private string[] _args;

        public string[] Args => _args??= new string[]{};

        public Command(Uuid uuid, string command, string[] args = null)
        {
            this._uuid = uuid;
            this._command = command;
            this._args = args;
        }

        public override string ToString()
        {
            return $"{Uuid}/{Cmd}{String.Join("/",Args)}";
        }

        public async void Execute(MiniserverContext? context, Action<string> action = null)
        {
            try
            {
                if (context != null)
                {
                    var res = await context.Connection.Command(default, this);
                    //if (action != null) await action.Invoke() - TODO support
                }
            }
            catch(MiniserverException ex)
            {
                context?.Logger.Log(LogLevel.Warning,$"Sending: {Uuid}->{Cmd}",ex);
            }
        }
    }
}
