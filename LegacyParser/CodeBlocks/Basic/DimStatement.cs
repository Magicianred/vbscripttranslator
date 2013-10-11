﻿using System;
using System.Text;
using System.Collections.Generic;
using VBScriptTranslator.LegacyParser.CodeBlocks.SourceRendering;

namespace VBScriptTranslator.LegacyParser.CodeBlocks.Basic
{
    [Serializable]
    public class DimStatement : ICodeBlock
    {
        // =======================================================================================
        // CLASS INITIALISATION
        // =======================================================================================
        private List<DimVariable> variables;
        public DimStatement(List<DimVariable> variables)
        {
            if (variables == null)
                throw new ArgumentNullException("variables");
            this.variables = variables;
        }

        // =======================================================================================
        // PUBLIC DATA ACCESS
        // =======================================================================================
        public List<DimVariable> Variables
        {
            get { return this.variables; }
        }

        // =======================================================================================
        // DESCRIPTION CLASSES
        // =======================================================================================
        public class DimVariable
        {
            private string name;
            private List<Expression> dimensions;
            public DimVariable(string name, List<Expression> dimensions)
            {
                if ((name ?? "").Trim() == "")
                    throw new ArgumentException("name is null or blank");
                this.name = name;
                this.dimensions = dimensions;
            }
            
            public string Name { get { return this.name; } }

            /// <summary>
            /// Variables list may be null (not explicitly defined as an array), have zero
            /// elements (an uninitialised array) or
            /// </summary>
            public List<Expression> Dimensions { get { return this.dimensions; } }

            public override string ToString()
            {
                return base.ToString() + ":" + this.name;
            }
        }

        // =======================================================================================
        // VBScript BASE SOURCE RE-GENERATION
        // =======================================================================================
        /// <summary>
        /// Re-generate equivalent VBScript source code for this block - there
        /// should not be a line return at the end of the content
        /// </summary>
        public virtual string GenerateBaseSource(SourceRendering.ISourceIndentHandler indenter)
        {
            StringBuilder output = new StringBuilder();
            output.Append(indenter.Indent);
            output.Append("Dim ");
            for (int index = 0; index < this.variables.Count; index++)
            {
                DimVariable variable = this.variables[index];
                output.Append(variable.Name);
                if (variable.Dimensions != null)
                {
                    output.Append("(");
                    for (int indexDimension = 0; indexDimension < variable.Dimensions.Count; indexDimension++)
                    {
                        Expression dimension = variable.Dimensions[indexDimension];
                        output.Append(dimension.GenerateBaseSource(new NullIndenter()));
                        if (indexDimension < (variable.Dimensions.Count - 1))
                            output.Append(", ");
                    }
                    output.Append(")");
                }
                if (index < (this.variables.Count - 1))
                    output.Append(", ");
            }
            return output.ToString();
        }
    }
}
