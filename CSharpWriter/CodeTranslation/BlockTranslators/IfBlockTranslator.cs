﻿using System;
using System.Linq;
using CSharpWriter.CodeTranslation.Extensions;
using CSharpWriter.CodeTranslation.StatementTranslation;
using CSharpWriter.Lists;
using CSharpWriter.Logging;
using VBScriptTranslator.LegacyParser.CodeBlocks;
using VBScriptTranslator.LegacyParser.CodeBlocks.Basic;

namespace CSharpWriter.CodeTranslation.BlockTranslators
{
    public class IfBlockTranslator : CodeBlockTranslator
    {
        private readonly ITranslateIndividualStatements _statementTranslator;
        private readonly ILogInformation _logger;
        public IfBlockTranslator(
            CSharpName supportRefName,
            CSharpName envClassName,
            CSharpName envRefName,
            CSharpName outerClassName,
            CSharpName outerRefName,
            VBScriptNameRewriter nameRewriter,
            TempValueNameGenerator tempNameGenerator,
			ITranslateIndividualStatements statementTranslator,
			ITranslateValueSettingsStatements valueSettingStatementTranslator,
            ILogInformation logger)
            : base(supportRefName, envClassName, envRefName, outerClassName, outerRefName, nameRewriter, tempNameGenerator, statementTranslator, valueSettingStatementTranslator, logger)
        {
            if (statementTranslator == null)
                throw new ArgumentNullException("statementTranslator");
            if (logger == null)
                throw new ArgumentNullException("logger");

            _statementTranslator = statementTranslator;
            _logger = logger;
        }

		public TranslationResult Translate(IfBlock ifBlock, ScopeAccessInformation scopeAccessInformation, int indentationDepth)
		{
			if (ifBlock == null)
                throw new ArgumentNullException("ifBlock");
			if (scopeAccessInformation == null)
				throw new ArgumentNullException("scopeAccessInformation");
            if (indentationDepth < 0)
                throw new ArgumentOutOfRangeException("indentationDepth", "must be zero or greater");

			var translationResult = TranslationResult.Empty;
            var numberOfAdditionalBlocksInjectedForErrorTrapping = 0;
            var previousBlockRequiredByRefArgumentRewriting = false;
            foreach (var conditionalEntry in ifBlock.ConditionalClauses.Select((conditional, index) => new { Conditional = conditional, Index = index }))
            {
                // This is the content that will ultimately be forced into a boolean and used to construct an "if" conditional, it it C# code. If there is no error-trapping
                // to worry about then we just have to wrap this in an IF call (the "IF" method in the runtime support class) and be done with it. If error-trapping MAY be
                // involved, though, then it's more complicated - we might be able to just use the IF extension method that takes an error registration token as an argument
                // (this is the second least-complicated code path) but if we're within a function (or property) and the there any function or property calls within this
                // translated content that take by-ref arguments and any of those arguments are by-ref arguments of the containing function, then it will have to be re-
                // written since C# will not allow "ref" arguments to be manipulated in lambas (and that is how by-ref arguments are dealt with when calling nested
                // functions or properties). This third arrangement is the most complicated code path.
                var conditionalContent = _statementTranslator.Translate(
                    conditionalEntry.Conditional.Condition,
                    scopeAccessInformation,
                    ExpressionReturnTypeOptions.NotSpecified,
                    _logger.Warning
                );

                // Check whether there are any "ref" arguments that need rewriting - this is only applicable if we're within a function or property that has ByRef arguments.
                // If this is the case then we need to ensure that we do not emit code that tries to include those references within a lambda since that is not valid C#. One
                // way in which this may occur is the passing of a "ref" argument into another function as ByRef as part of the condition evaluation (since the argument provider
                // will update the value after the call completes using a lambda). The other way is if error-trapping might be enabled at runtime - in this case, the evaluation
                // of the condition is performed within a lambda so that any errors can be swallowed if necessary. If any such reference rewriting is required then the code that
                // must be emitted is more complex.
                var byRefArgumentIdentifier = new FuncByRefArgumentMapper(_nameRewriter, _tempNameGenerator, _logger);
                var byRefArgumentsToRewrite = byRefArgumentIdentifier.GetByRefArgumentsThatNeedRewriting(
                    conditionalEntry.Conditional.Condition.ToStageTwoParserExpression(scopeAccessInformation, ExpressionReturnTypeOptions.NotSpecified, _logger.Warning),
                    scopeAccessInformation,
                    new NonNullImmutableList<FuncByRefMapping>()
                );
                if (byRefArgumentsToRewrite.Any())
                {
                    // If we're in a function or property and that function / property has by-ref arguments that we then need to pass into further function / property calls
                    // in order to evaluate the current conditional, then we need to record those values in temporary references, try to evaulate the condition and then push
                    // the temporary values back into the original references. This is required in order to be consistent with VBScript and yet also produce code that compiles
                    // as C# (which will not let "ref" arguments of the containing function be used in lambdas, which is how we deal with updating by-ref arguments after function
                    // or property calls complete).
                    var evaluatedResultName = _tempNameGenerator(new CSharpName("ifResult"), scopeAccessInformation);
                    scopeAccessInformation = scopeAccessInformation.ExtendVariables(
                        byRefArgumentsToRewrite
                            .Select(r => new ScopedNameToken(r.To.Name, r.From.LineIndex, ScopeLocationOptions.WithinFunctionOrPropertyOrWith))
                            .ToNonNullImmutableList()
                    );
                    translationResult = translationResult.Add(new TranslatedStatement(
                        "bool " + evaluatedResultName.Name + ";",
                        indentationDepth
                    ));
                    var byRefMappingOpeningTranslationDetails = byRefArgumentsToRewrite.OpenByRefReplacementDefinitionWork(translationResult, indentationDepth, _nameRewriter);
                    translationResult = byRefMappingOpeningTranslationDetails.TranslationResult;
                    indentationDepth += byRefMappingOpeningTranslationDetails.DistanceToIndentCodeWithMappedValues;
                    var rewrittenConditionalContent = _statementTranslator.Translate(
                        byRefArgumentsToRewrite.RewriteExpressionUsingByRefArgumentMappings(conditionalEntry.Conditional.Condition, _nameRewriter),
                        scopeAccessInformation,
                        ExpressionReturnTypeOptions.NotSpecified,
                        _logger.Warning
                    );
                    var ifStatementFormat = (scopeAccessInformation.ErrorRegistrationTokenIfAny == null)
                        ? "{0} = {1}.IF({2});"
                        : "{0} = {1}.IF(() => {2}, {3});";
                    translationResult = translationResult.Add(new TranslatedStatement(
                        string.Format(
                            ifStatementFormat,
                            evaluatedResultName.Name,
                            _supportRefName.Name,
                            rewrittenConditionalContent.TranslatedContent,
                            (scopeAccessInformation.ErrorRegistrationTokenIfAny == null) ? "" : scopeAccessInformation.ErrorRegistrationTokenIfAny.Name
                        ),
                        indentationDepth
                    ));
                    indentationDepth -= byRefMappingOpeningTranslationDetails.DistanceToIndentCodeWithMappedValues;
                    translationResult = byRefArgumentsToRewrite.CloseByRefReplacementDefinitionWork(translationResult, indentationDepth, _nameRewriter);
                    conditionalContent = new TranslatedStatementContentDetails(
                        evaluatedResultName.Name,
                        rewrittenConditionalContent.VariablesAccessed
                    );
                }
                else if (scopeAccessInformation.MayRequireErrorWrapping(ifBlock))
                {
                    // If we're not in a function or property or if that function or property does not have any by-ref arguments that we need to pass in as by-ref arguments
                    // to further functions or properties, then we're in the less complicate error-trapping scenario; we only have to use the IF extension method that deals
                    // with error-trapping.
                    conditionalContent = new TranslatedStatementContentDetails(
                        string.Format(
                            "{0}.IF(() => {1}, {2})",
                            _supportRefName.Name,
                            conditionalContent.TranslatedContent,
                            scopeAccessInformation.ErrorRegistrationTokenIfAny.Name
                        ),
                        conditionalContent.VariablesAccessed
                    );
                }
                else
                {
                    conditionalContent = new TranslatedStatementContentDetails(
                        string.Format(
                            "{0}.IF({1})",
                            _supportRefName.Name,
                            conditionalContent.TranslatedContent
                        ),
                        conditionalContent.VariablesAccessed
                    );
                }

                var undeclaredVariablesAccessed = conditionalContent.GetUndeclaredVariablesAccessed(scopeAccessInformation, _nameRewriter);
                foreach (var undeclaredVariable in undeclaredVariablesAccessed)
                    _logger.Warning("Undeclared variable: \"" + undeclaredVariable.Content + "\" (line " + (undeclaredVariable.LineIndex + 1) + ")");
                translationResult = translationResult.AddUndeclaredVariables(undeclaredVariablesAccessed);
                var innerStatements = conditionalEntry.Conditional.Statements.ToNonNullImmutableList();
                var conditionalInlineCommentIfAny = !innerStatements.Any() ? null : (innerStatements.First() as InlineCommentStatement);
                if (conditionalInlineCommentIfAny != null)
                    innerStatements = innerStatements.RemoveAt(0);
                // Note: For details on why previousBlockRequiredByRefArgumentRewriting is consulted in order to determine whether to render "if" or "else" if, see the notes further
                // down, where the value of the previousBlockRequiredByRefArgumentRewriting flag is set
                translationResult = translationResult.Add(
                    new TranslatedStatement(
                        string.Format(
                            "{0} ({1}){2}",
                            (conditionalEntry.Index == 0) || previousBlockRequiredByRefArgumentRewriting ? "if" : "else if",
                            conditionalContent.TranslatedContent,
                            (conditionalInlineCommentIfAny == null) ? "" : (" //" + conditionalInlineCommentIfAny.Content)
                        ),
                        indentationDepth
                    )
                );
                translationResult = translationResult.Add(new TranslatedStatement("{", indentationDepth));
                translationResult = translationResult.Add(
                    Translate(
                        innerStatements,
                        scopeAccessInformation.SetParent(ifBlock),
                        indentationDepth + 1
                    )
                );
                translationResult = translationResult.Add(new TranslatedStatement("}", indentationDepth));

                // If we're dealing with multiple if (a).. elseif (b).. elseif (c).. [else] blocks then these would be most simply represented by if (a).. else if (b)..
                // else if (c).. [else] blocks in C#. However, if error-trapping is involved then some of the conditions may have to be rewritten to deal with by-ref arguments
                // and then (after the condition is evaluated) those rewritten arguments need to be reflected on the original references (see the note further up for more
                // details). In this case, each subsequent "if" condition must be within its own "else" block in order for the the rewritten condition to be evaluated when
                // required (and not before). When this happens, there will be greater levels of nesting required. This nesting is injected here and tracked with the variable
                // "numberOfAdditionalBlocksInjectedForErrorTrapping" (this will be used further down to ensure that any extra levels of nesting are closed off).
                var isLastConditionalBlock = (conditionalEntry.Index == (ifBlock.ConditionalClauses.Count() - 1));
                if (byRefArgumentsToRewrite.Any() && !isLastConditionalBlock)
                {
                    translationResult = translationResult.Add(new TranslatedStatement("else", indentationDepth));
                    translationResult = translationResult.Add(new TranslatedStatement("{", indentationDepth));
                    indentationDepth++;
                    numberOfAdditionalBlocksInjectedForErrorTrapping++;
                }
                
                // It's important to know whether the previous block (if any) required any "ref" arguments to be rewritten since this will change the format of the translated
                // code. If there is no rewriting required then an "if (a).. elseif (b).. else.." structure can be translated into a very similar "if (a) { .. } else if (b)
                // { .. } else { .. }" format, but if there are argument aliases that need creating and potentially updating after the condition is evaluated then each condition
                // is no longer on a single line and will be translated into something more like "if (a) { .. } else { if (b) { .. } else { .. } }". This makes it harder to
                // read the output but it required in order to perform the multiple steps required at each condition evaluation where rewriting is required (it is no longer
                // "just evaluate the condition", instead it becomes "initialise any aliases, evaluate the condition using the aliases, update the original references if
                // required").
                previousBlockRequiredByRefArgumentRewriting = byRefArgumentsToRewrite.Any();
            }

            if (ifBlock.OptionalElseClause != null)
            {
                translationResult = translationResult.Add(new TranslatedStatement("else", indentationDepth));
                translationResult = translationResult.Add(new TranslatedStatement("{", indentationDepth));
                translationResult = translationResult.Add(
                    Translate(
                        ifBlock.OptionalElseClause.Statements.ToNonNullImmutableList(),
                        scopeAccessInformation.SetParent(ifBlock),
                        indentationDepth + 1
                    )
                );
                translationResult = translationResult.Add(new TranslatedStatement("}", indentationDepth));
            }

            // If any additional levels of nesting were required above (for error-trapping scenarios), ensure they are closed off here
            for (var index = 0; index < numberOfAdditionalBlocksInjectedForErrorTrapping; index++)
            {
                indentationDepth--;
                translationResult = translationResult.Add(new TranslatedStatement("}", indentationDepth));
            }

            return translationResult;
		}

		private TranslationResult Translate(NonNullImmutableList<ICodeBlock> blocks, ScopeAccessInformation scopeAccessInformation, int indentationDepth)
		{
			if (blocks == null)
				throw new ArgumentNullException("block");
			if (scopeAccessInformation == null)
				throw new ArgumentNullException("scopeAccessInformation");
			if (indentationDepth < 0)
				throw new ArgumentOutOfRangeException("indentationDepth", "must be zero or greater");

			return base.TranslateCommon(
                base.GetWithinFunctionBlockTranslators(),
				blocks,
				scopeAccessInformation,
				indentationDepth
			);
		}
    }
}
