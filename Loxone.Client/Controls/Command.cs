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
    using System.Threading.Tasks;

    public class Command
    {
        private string[] _args;

        public Command(Uuid uuid, string command, string[] args = null)
        {
            this.Uuid = uuid;
            this.Cmd = command;
            this._args = args;
        }

        public Uuid Uuid { get; }
        public string Cmd { get; }
        public string[] Args => _args ??= new string[] { };

        public override string ToString() => $"{Uuid}/{Cmd}{String.Join("/", Args)}";

        public async Task ExecuteAsync(MiniserverContext context, Func<string,int,object,Task> action = null)
        {
                if (context != null)
                {
                    var res = await context?.Connection.Command(default, this);
                    await action?.Invoke(res?.Value, res.Code, context?.ContextParent);
                }
        }
    }
}
