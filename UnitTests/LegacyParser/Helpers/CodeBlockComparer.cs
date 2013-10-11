﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using VBScriptTranslator.LegacyParser.CodeBlocks;
using VBScriptTranslator.LegacyParser.CodeBlocks.Basic;
using VBScriptTranslator.LegacyParser.Tokens;

namespace VBScriptTranslator.UnitTests.LegacyParser.Helpers
{
    public class CodeBlockComparer : IEqualityComparer<ICodeBlock>
    {
        public bool Equals(ICodeBlock x, ICodeBlock y)
        {
            if (x == null)
                throw new ArgumentNullException("x");
            if (y == null)
                throw new ArgumentNullException("y");

            if (x.GetType() != y.GetType())
                return false;

            var tokenSetComparer = new TokenSetComparer();

            if (x.GetType() == typeof(Statement))
                return tokenSetComparer.Equals(((Statement)x).Tokens, ((Statement)y).Tokens);
            else if (x.GetType() == typeof(ValueSettingStatement))
            {
                var valueSettingStatementX = (ValueSettingStatement)x;
                var valueSettingStatementY = (ValueSettingStatement)y;
                return (
                    (valueSettingStatementX.ValueSetType == valueSettingStatementY.ValueSetType) &&
                    tokenSetComparer.Equals(valueSettingStatementX.ValueToSetTokens, valueSettingStatementY.ValueToSetTokens) &&
                    tokenSetComparer.Equals(valueSettingStatementX.ExpressionTokens, valueSettingStatementY.ExpressionTokens)
                );
            }

            throw new NotSupportedException("Can not compare ICodeBlock of type " + x.GetType());
        }

        public int GetHashCode(ICodeBlock obj)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");

            return 0;
        }

    }
}
