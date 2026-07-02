using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace DB3DFloora
{
    public class FlooraCommand : Command
    {
        public FlooraCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static FlooraCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "Floora";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            FlooraForm.Show_();
            return Result.Success;
        }
    }
}
