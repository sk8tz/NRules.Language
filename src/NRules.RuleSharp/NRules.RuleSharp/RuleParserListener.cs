﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NRules.RuleModel;
using NRules.RuleModel.Builders;
using NRules.RuleSharp.Parser;
using static NRules.RuleSharp.Parser.RuleSharpParser;

namespace NRules.RuleSharp
{
    internal class RuleParserListener : RuleSharpParserBaseListener
    {
        private readonly ParserContext _parserContext;
        private readonly RuleBuilder _builder;
        private readonly GroupBuilder _groupBuilder;
        private readonly ActionGroupBuilder _actionGroupBuilder;

        public RuleParserListener(ParserContext parserContext, RuleBuilder builder)
        {
            _parserContext = parserContext;
            _builder = builder;
            _groupBuilder = builder.LeftHandSide();
            _actionGroupBuilder = builder.RightHandSide();
        }

        public override void EnterRule_name(Rule_nameContext context)
        {
            var value = context.GetText();
            var name = value.TrimStart('@').Trim('"');
            _builder.Name(name);
        }

        public override void EnterRule_description(Rule_descriptionContext context)
        {
            var value = context.value.GetText();
            var description = value.TrimStart('@').Trim('"');
            _builder.Description(description);
        }

        public override void EnterRule_priority(Rule_priorityContext context)
        {
            var value = context.value.Text;
            var priotity = Int32.Parse(value);
            _builder.Priority(priotity);
        }

        public override void EnterRule_tags(Rule_tagsContext context)
        {
            var tags = context._values.Select(x => x.GetText().TrimStart('@').Trim('"'));
            _builder.Tags(tags);
        }

        public override void EnterRuleFactMatch(RuleFactMatchContext context)
        {
            var patternTypeName = context.type().GetText();
            var patternType = _parserContext.FindType(patternTypeName);
            if (patternType == null)
                throw new ParseException($"Unknown type. Type={patternTypeName}", context);

            var variableTypeName = context.local_variable_type().VAR() == null
                ? context.local_variable_type().type().GetText()
                : patternTypeName;
            var variableType = _parserContext.FindType(variableTypeName);
            if (variableType == null)
                throw new ParseException($"Unknown type. Type={variableTypeName}", context);

            var id = context.identifier().GetText();
            _parserContext.Scope.Declare(variableType, id);

            var patternBuilder = _groupBuilder.Pattern(patternType, id);
            if (context.expression_list() != null)
            {
                foreach (var expressionContext in context.expression_list().expression())
                {
                    using (_parserContext.PushScope())
                    {
                        var expressionParser = new ExpressionParser(_parserContext, patternType);
                        var expression = (LambdaExpression) expressionParser.Visit(expressionContext);
                        patternBuilder.DslConditions(_groupBuilder.Declarations, expression);
                    }
                }
            }
        }

        public override void EnterRuleExistsMatch(RuleExistsMatchContext context)
        {
            var patternTypeName = context.type().GetText();
            var patternType = _parserContext.FindType(patternTypeName);
            if (patternType == null)
                throw new ParseException($"Unknown type. Type={patternTypeName}", context);

            var existsBuilder = _groupBuilder.Exists();
            var patternBuilder = existsBuilder.Pattern(patternType);
            if (context.expression_list() != null)
            {
                foreach (var expressionContext in context.expression_list().expression())
                {
                    using (_parserContext.PushScope())
                    {
                        var expressionParser = new ExpressionParser(_parserContext, patternType);
                        var expression = (LambdaExpression) expressionParser.Visit(expressionContext);
                        patternBuilder.DslConditions(_groupBuilder.Declarations, expression);
                    }
                }
            }
        }

        public override void EnterRuleNotMatch(RuleNotMatchContext context)
        {
            var patternTypeName = context.type().GetText();
            var patternType = _parserContext.FindType(patternTypeName);
            if (patternType == null)
                throw new ParseException($"Unknown type. Type={patternTypeName}", context);

            var existsBuilder = _groupBuilder.Not();
            var patternBuilder = existsBuilder.Pattern(patternType);
            if (context.expression_list() != null)
            {
                foreach (var expressionContext in context.expression_list().expression())
                {
                    using (_parserContext.PushScope())
                    {
                        var expressionParser = new ExpressionParser(_parserContext, patternType);
                        var expression = (LambdaExpression) expressionParser.Visit(expressionContext);
                        patternBuilder.DslConditions(_groupBuilder.Declarations, expression);
                    }
                }
            }
        }

        public override void EnterRule_action(Rule_actionContext context)
        {
            var contextParameter = Expression.Parameter(typeof(IContext), "Context");
            var parameters = new List<ParameterExpression>{contextParameter};
            parameters.AddRange(_parserContext.Scope.Values);

            using (_parserContext.PushScope())
            {
                _parserContext.Scope.Declare(contextParameter);

                var expressionParser = new ExpressionParser(_parserContext);
                var block = expressionParser.Visit(context.statement_list());
                var lambda = Expression.Lambda(block, parameters);
                _actionGroupBuilder.DslAction(_actionGroupBuilder.Declarations, lambda);
            }
        }
    }
}