﻿using System;
using System.Collections.Generic;
using VBScriptTranslator.LegacyParser.Tokens.Basic;

namespace VBScriptTranslator.LegacyParser.CodeBlocks.Basic
{
    [Serializable]
    public class SubBlock : AbstractFunctionBlock
    {
        public SubBlock(
            bool isPublic,
            bool isDefault,
            NameToken name,
            List<Parameter> parameters,
            List<ICodeBlock> statements)
            : base(isPublic, isDefault, name, parameters, statements) { }

        protected override string keyWord
        {
            get { return "Sub"; }
        }
    }
}
