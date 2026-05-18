using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Application.Logging;
using Game.Core.Application.Periods;

namespace Game.Core.Application.Session
{
    public sealed class SessionConfigRuntimeBuilder : ISessionConfigRuntimeBuilder
    {
        private readonly IAppLogger _logger;

        public SessionConfigRuntimeBuilder(IAppLogger logger)
        {
            _logger = logger;
        }

        public bool TryBuild(
            ParsedJsonDocument document,
            out SessionConfigRuntime runtime,
            out string error)
        {
            runtime = SessionConfigRuntime.Empty;
            error = string.Empty;

            if (document == null || document.IsEmpty)
            {
                runtime = SessionConfigRuntime.Empty;
                return true;
            }

            if (!document.IsValid)
            {
                error = string.IsNullOrWhiteSpace(document.ParseError)
                    ? "Session config JSON is invalid."
                    : document.ParseError;
                runtime = new SessionConfigRuntime(
                    0,
                    GlobalSettingsRuntime.Empty,
                    SessionFlowRuntime.Empty,
                    SessionFlowRuntime.Empty,
                    Array.Empty<SharedSurveyRefRuntime>(),
                    Array.Empty<SessionPeriodRuntime>(),
                    JsonValue.Null,
                    false,
                    error);
                return false;
            }

            if (document.Root.Kind != JsonValueKind.Object)
            {
                error = "Session config root must be an object.";
                runtime = new SessionConfigRuntime(
                    0,
                    GlobalSettingsRuntime.Empty,
                    SessionFlowRuntime.Empty,
                    SessionFlowRuntime.Empty,
                    Array.Empty<SharedSurveyRefRuntime>(),
                    Array.Empty<SessionPeriodRuntime>(),
                    document.Root,
                    false,
                    error);
                return false;
            }

            var root = document.Root;
            var schemaVersion = ReadInt(root, "schemaVersion", "version");
            var globalSettingsNode = root.GetObjectCandidate("globalSettings", "settings");
            var periodsNode = root.GetArrayCandidate("periods", "periodDefinitions", "periodConfigs");
            var periods = ParsePeriods(periodsNode);
            var preSessionFlow = ParseFlowContainer(root.FindFirstProperty("preSessionFlow", "preFlow"), "pre");
            var postSessionFlow = ParseFlowContainer(root.FindFirstProperty("postSessionFlow", "postFlow"), "post");
            var sharedSurveyRefs = ParseSharedSurveyRefs(root.FindFirstProperty("sharedSurveyRefs", "surveyRefs"));
            var globalSettings = ParseGlobalSettings(schemaVersion, globalSettingsNode);

            var isValid = periods.Count > 0 || preSessionFlow.HasSteps || postSessionFlow.HasSteps;

            if (!isValid)
            {
                error = "Session config does not contain any runnable flow steps or periods.";
                _logger.Warning(error);
            }

            runtime = new SessionConfigRuntime(
                schemaVersion,
                globalSettings,
                preSessionFlow,
                postSessionFlow,
                sharedSurveyRefs,
                periods,
                root,
                isValid,
                error);

            return isValid;
        }

        private static GlobalSettingsRuntime ParseGlobalSettings(int schemaVersion, JsonValue node)
        {
            var currencyCode = ReadString(node, "currencyCode", "currency", "moneyUnit");
            var baseIncome = ReadNumber(node, "baseIncomeEcu", "baseIncome", "startingIncomeEcu");
            var allowOptionalSkip = ReadBoolean(node, "allowOptionalSkip", "allowSkippingOptionalSteps");

            return new GlobalSettingsRuntime(
                node,
                string.IsNullOrWhiteSpace(currencyCode) ? "ECU" : currencyCode,
                baseIncome,
                allowOptionalSkip);
        }

        private static SessionFlowRuntime ParseFlowContainer(JsonValue node, string prefix)
        {
            if (node == null || node.Kind == JsonValueKind.Null)
            {
                return SessionFlowRuntime.Empty;
            }

            if (node.Kind == JsonValueKind.Array)
            {
                return new SessionFlowRuntime(
                    true,
                    string.Empty,
                    ParseFlowSteps(node.ArrayValue, prefix));
            }

            if (node.Kind != JsonValueKind.Object)
            {
                return SessionFlowRuntime.Empty;
            }

            var stepsNode = node.FindFirstProperty("steps", "items", "blocks");
            var steps = stepsNode.Kind == JsonValueKind.Array
                ? ParseFlowSteps(stepsNode.ArrayValue, prefix)
                : Array.Empty<FlowStepRuntime>();
            var enabled = ReadBoolean(node, "enabled", "isEnabled") ?? steps.Count > 0;

            return new SessionFlowRuntime(
                enabled,
                ReadString(node, "title", "label", "name"),
                steps);
        }

        private static IReadOnlyList<FlowStepRuntime> ParseFlowSteps(IReadOnlyList<JsonValue> nodes, string prefix)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return Array.Empty<FlowStepRuntime>();
            }

            var result = new List<FlowStepRuntime>(nodes.Count);

            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];

                if (node == null || node.Kind != JsonValueKind.Object)
                {
                    continue;
                }

                var rawType = ReadString(node, "type", "stepType", "kind", "contentType");
                var type = ParseStepType(rawType);
                var contentNode = ResolveContentNode(node);
                var title = ReadString(node, "title", "label", "name", "heading");

                if (string.IsNullOrWhiteSpace(title) && contentNode.Kind == JsonValueKind.Object)
                {
                    title = ReadString(contentNode, "title", "label", "name", "heading");
                }

                var stepId = BuildKey(prefix, index, ReadString(node, "id", "code", "key", "internalCode", "ref"));
                var surveyRef = ParseSurveyRef(node.FindFirstProperty("surveyRef", "survey", "surveyTemplate", "template"));

                if (surveyRef.IsEmpty
                    && IsSurveyStepType(type))
                {
                    surveyRef = ParseSurveyRef(node);
                }

                result.Add(new FlowStepRuntime(
                    stepId,
                    type,
                    title,
                    ReadStepSubtitle(node, contentNode),
                    ReadStepBody(node, contentNode),
                    ReadBoolean(node, "required", "isRequired", "mandatory") ?? true,
                    ReadString(node, "internalCode", "code"),
                    surveyRef,
                    node));
            }

            return result;
        }

        private static IReadOnlyList<SharedSurveyRefRuntime> ParseSharedSurveyRefs(JsonValue node)
        {
            if (node == null || node.Kind != JsonValueKind.Array)
            {
                return Array.Empty<SharedSurveyRefRuntime>();
            }

            var result = new List<SharedSurveyRefRuntime>(node.Count);

            for (var index = 0; index < node.ArrayValue.Count; index++)
            {
                var item = node.ArrayValue[index];

                if (item == null)
                {
                    continue;
                }

                var id = ReadString(item, "id", "refId", "code", "key");
                var survey = ParseSurveyRef(item);

                if (string.IsNullOrWhiteSpace(id) && survey.IsEmpty)
                {
                    continue;
                }

                result.Add(new SharedSurveyRefRuntime(
                    string.IsNullOrWhiteSpace(id)
                        ? !string.IsNullOrWhiteSpace(survey.Id)
                            ? survey.Id
                            : !string.IsNullOrWhiteSpace(survey.SharedRefId)
                                ? survey.SharedRefId
                                : survey.TemplateCode
                        : id,
                    survey));
            }

            return result;
        }

        private static IReadOnlyList<SessionPeriodRuntime> ParsePeriods(JsonValue periodsNode)
        {
            if (periodsNode == null || periodsNode.Kind != JsonValueKind.Array)
            {
                return Array.Empty<SessionPeriodRuntime>();
            }

            var result = new List<SessionPeriodRuntime>(periodsNode.Count);
            var fallbackNumber = 1;

            for (var index = 0; index < periodsNode.ArrayValue.Count; index++)
            {
                var node = periodsNode.ArrayValue[index];

                if (node == null || node.Kind != JsonValueKind.Object)
                {
                    continue;
                }

                var number = ReadInt(node, "number", "periodNumber", "index", "id");

                if (number <= 0)
                {
                    number = fallbackNumber;
                }

                fallbackNumber = Math.Max(fallbackNumber + 1, number + 1);

                var period = new SessionPeriodRuntime(
                    number,
                    ReadString(node, "title", "label", "name"),
                    ReadString(node, "historicalYear", "year", "historicalLabel"),
                    ReadString(node, "phase", "stage"),
                    ParseStringArray(node.FindFirstProperty("enabledFeatures", "features")),
                    ParseInfoBlock(node.FindFirstProperty("infoBlock", "info", "periodInfo")),
                    ParsePeriodContentBlocks(node.FindFirstProperty("periodContentBlocks", "contentBlocks", "periodBlocks"), number),
                    ParseSurveyRefs(node.FindFirstProperty("postPeriodSurveyRefs", "postPeriodSurveys", "periodSurveyRefs"), number),
                    ParseInterPeriodBlocks(node.FindFirstProperty("interPeriodBlocks", "betweenPeriodBlocks"), number),
                    ReadString(node, "internalCode", "code"),
                    node);

                result.Add(period);
            }

            result.Sort((left, right) => left.Number.CompareTo(right.Number));
            return result;
        }

        private static PeriodInfoBlockRuntime ParseInfoBlock(JsonValue node)
        {
            if (node == null || node.Kind == JsonValueKind.Null)
            {
                return PeriodInfoBlockRuntime.Empty;
            }

            var result = new List<PeriodInfoEntryRuntime>();

            if (node.Kind == JsonValueKind.Array)
            {
                for (var index = 0; index < node.ArrayValue.Count; index++)
                {
                    var item = node.ArrayValue[index];

                    if (item == null)
                    {
                        continue;
                    }

                    var id = ReadString(item, "id", "code", "key");
                    var label = ReadString(item, "label", "title", "name");
                    var numericValue = ReadNumber(item, "value", "numericValue", "amount");
                    var rawText = numericValue.HasValue ? string.Empty : ReadString(item, "value", "text", "rawText");
                    var suffix = ReadString(item, "suffix", "unit");

                    if (string.IsNullOrWhiteSpace(id))
                    {
                        id = BuildKey("info", index, label);
                    }

                    result.Add(new PeriodInfoEntryRuntime(id, label, numericValue, rawText, suffix));
                }
            }
            else if (node.Kind == JsonValueKind.Object)
            {
                foreach (var pair in node.ObjectValue)
                {
                    var id = pair.Key ?? string.Empty;
                    var label = ReadString(pair.Value, "label", "title", "name");

                    if (string.IsNullOrWhiteSpace(label))
                    {
                        label = HumanizeKey(id);
                    }

                    var numericValue = pair.Value.AsNullableNumber();
                    var rawText = numericValue.HasValue ? string.Empty : pair.Value.GetStringOrDefault();
                    var suffix = ReadString(pair.Value, "suffix", "unit");

                    result.Add(new PeriodInfoEntryRuntime(id, label, numericValue, rawText, suffix));
                }
            }

            return new PeriodInfoBlockRuntime(result, node);
        }

        private static IReadOnlyList<PeriodContentBlockRuntime> ParsePeriodContentBlocks(JsonValue node, int periodNumber)
        {
            if (node == null || node.Kind != JsonValueKind.Array)
            {
                return Array.Empty<PeriodContentBlockRuntime>();
            }

            var result = new List<PeriodContentBlockRuntime>(node.Count);

            for (var index = 0; index < node.ArrayValue.Count; index++)
            {
                var item = node.ArrayValue[index];

                if (item == null || item.Kind != JsonValueKind.Object)
                {
                    continue;
                }

                result.Add(new PeriodContentBlockRuntime(
                    BuildKey($"period_{periodNumber}_content", index, ReadString(item, "id", "code", "key", "internalCode")),
                    ParseStepType(ReadString(item, "type", "kind", "contentType")),
                    ReadString(item, "title", "label", "name", "heading"),
                    ReadString(item, "subtitle", "summary", "caption"),
                    ReadBody(item),
                    ReadBoolean(item, "required", "isRequired", "mandatory") ?? true,
                    ReadString(item, "internalCode", "code"),
                    item));
            }

            return result;
        }

        private static IReadOnlyList<SurveyRefRuntime> ParseSurveyRefs(JsonValue node, int periodNumber)
        {
            if (node == null || node.Kind != JsonValueKind.Array)
            {
                return Array.Empty<SurveyRefRuntime>();
            }

            var result = new List<SurveyRefRuntime>(node.Count);

            for (var index = 0; index < node.ArrayValue.Count; index++)
            {
                var survey = ParseSurveyRef(node.ArrayValue[index]);

                if (survey.IsEmpty)
                {
                    continue;
                }

                var surveyId = string.IsNullOrWhiteSpace(survey.Id)
                    ? BuildKey($"period_{periodNumber}_survey", index, survey.TemplateCode)
                    : survey.Id;
                result.Add(new SurveyRefRuntime(
                    surveyId,
                    survey.SharedRefId,
                    survey.TemplateCode,
                    survey.TemplateId,
                    string.IsNullOrWhiteSpace(survey.Purpose) ? "post_period" : survey.Purpose,
                    survey.Title,
                    survey.IsRequired));
            }

            return result;
        }

        private static IReadOnlyList<InterPeriodBlockRuntime> ParseInterPeriodBlocks(JsonValue node, int periodNumber)
        {
            if (node == null || node.Kind != JsonValueKind.Array)
            {
                return Array.Empty<InterPeriodBlockRuntime>();
            }

            var result = new List<InterPeriodBlockRuntime>(node.Count);

            for (var index = 0; index < node.ArrayValue.Count; index++)
            {
                var item = node.ArrayValue[index];

                if (item == null || item.Kind != JsonValueKind.Object)
                {
                    continue;
                }

                result.Add(new InterPeriodBlockRuntime(
                    BuildKey($"period_{periodNumber}_inter", index, ReadString(item, "id", "code", "key", "internalCode")),
                    ParseStepType(ReadString(item, "type", "kind", "contentType")),
                    ReadString(item, "title", "label", "name", "heading"),
                    ReadString(item, "subtitle", "summary", "caption"),
                    ReadBody(item),
                    ReadBoolean(item, "required", "isRequired", "mandatory") ?? true,
                    ReadString(item, "internalCode", "code"),
                    item));
            }

            return result;
        }

        private static SurveyRefRuntime ParseSurveyRef(JsonValue node)
        {
            if (node == null || node.Kind == JsonValueKind.Null)
            {
                return SurveyRefRuntime.Empty;
            }

            if (node.Kind == JsonValueKind.String)
            {
                var value = node.StringValue ?? string.Empty;
                return new SurveyRefRuntime(
                    value,
                    value,
                    value,
                    null,
                    string.Empty,
                    string.Empty,
                    true);
            }

            if (node.Kind != JsonValueKind.Object)
            {
                return SurveyRefRuntime.Empty;
            }

            var templateId = ReadInt(node, "templateId", "id");
            var directRef = ReadString(node, "ref", "surveyRefId");
            var templateCode = ReadString(node, "templateCode", "surveyCode", "code");

            if (string.IsNullOrWhiteSpace(templateCode))
            {
                templateCode = directRef;
            }

            return new SurveyRefRuntime(
                ReadString(node, "id", "refId", "code", "key"),
                ReadString(node, "sharedRefId", "sharedSurveyRefId", "ref", "surveyRefId"),
                templateCode,
                templateId > 0 ? (int?)templateId : null,
                ReadString(node, "purpose", "type"),
                ReadString(node, "title", "label", "name"),
                ReadBoolean(node, "required", "isRequired", "mandatory") ?? true);
        }

        private static SessionFlowStepType ParseStepType(string rawValue)
        {
            var normalized = Normalize(rawValue);

            switch (normalized)
            {
                case "instruction":
                    return SessionFlowStepType.Instruction;
                case "instructionquiz":
                    return SessionFlowStepType.InstructionQuiz;
                case "pretest":
                    return SessionFlowStepType.PreTest;
                case "posttest":
                    return SessionFlowStepType.PostTest;
                case "postperiodsurvey":
                case "postperiod":
                case "survey":
                    return SessionFlowStepType.PostPeriodSurvey;
                case "instructionalpopup":
                    return SessionFlowStepType.InstructionalPopup;
                case "news":
                    return SessionFlowStepType.News;
                default:
                    return SessionFlowStepType.Unknown;
            }
        }

        private static bool IsSurveyStepType(SessionFlowStepType type)
        {
            switch (type)
            {
                case SessionFlowStepType.InstructionQuiz:
                case SessionFlowStepType.PreTest:
                case SessionFlowStepType.PostTest:
                case SessionFlowStepType.PostPeriodSurvey:
                    return true;
                default:
                    return false;
            }
        }

        private static JsonValue ResolveContentNode(JsonValue node)
        {
            if (node == null || node.Kind != JsonValueKind.Object)
            {
                return JsonValue.Null;
            }

            return node.GetObjectCandidate("contentBlock", "content", "block");
        }

        private static string ReadStepSubtitle(JsonValue node, JsonValue contentNode)
        {
            var subtitle = ReadString(node, "subtitle", "summary", "caption");

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                return subtitle;
            }

            return contentNode != null && contentNode.Kind == JsonValueKind.Object
                ? ReadString(contentNode, "subtitle", "summary", "caption")
                : string.Empty;
        }

        private static string ReadStepBody(JsonValue node, JsonValue contentNode)
        {
            var body = ReadBody(node);

            if (!string.IsNullOrWhiteSpace(body))
            {
                return body;
            }

            return contentNode != null && contentNode.Kind == JsonValueKind.Object
                ? ReadBody(contentNode)
                : string.Empty;
        }

        private static string ReadBody(JsonValue node)
        {
            var direct = ReadString(node, "body", "text", "content", "description", "message", "markdown");

            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }

            if (node.TryGetPropertyIgnoreCase("payload", out var payloadNode)
                && payloadNode.Kind == JsonValueKind.Object)
            {
                var payloadText = ReadString(payloadNode, "text", "body", "description", "message", "markdown");

                if (!string.IsNullOrWhiteSpace(payloadText))
                {
                    return payloadText;
                }
            }

            if (node.TryGetPropertyIgnoreCase("content", out var contentNode)
                && contentNode.Kind == JsonValueKind.Object)
            {
                return ReadString(contentNode, "body", "text", "description", "message");
            }

            return string.Empty;
        }

        private static IReadOnlyList<string> ParseStringArray(JsonValue node)
        {
            if (node == null || node.Kind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(node.Count);

            for (var index = 0; index < node.ArrayValue.Count; index++)
            {
                var item = node.ArrayValue[index];
                var value = item.GetStringOrDefault();

                if (string.IsNullOrWhiteSpace(value))
                {
                    value = ReadString(item, "id", "code", "key", "name");
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                result.Add(value.Trim());
            }

            return result;
        }

        private static string BuildKey(string prefix, int index, string rawValue)
        {
            if (!string.IsNullOrWhiteSpace(rawValue))
            {
                return $"{prefix}:{Normalize(rawValue)}";
            }

            return $"{prefix}:{index + 1}";
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var result = value.Trim().ToLowerInvariant();
            result = result.Replace("_", string.Empty);
            result = result.Replace("-", string.Empty);
            result = result.Replace(" ", string.Empty);
            return result;
        }

        private static string HumanizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Replace("_", " ").Replace("-", " ").Trim();

            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
        }

        private static string ReadString(JsonValue node, params string[] propertyNames)
        {
            if (node == null || propertyNames == null)
            {
                return string.Empty;
            }

            if (node.Kind == JsonValueKind.String)
            {
                return node.StringValue ?? string.Empty;
            }

            foreach (var propertyName in propertyNames)
            {
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    continue;
                }

                if (node.TryGetPropertyIgnoreCase(propertyName, out var value))
                {
                    if (value.Kind == JsonValueKind.String)
                    {
                        return value.StringValue ?? string.Empty;
                    }

                    if (value.Kind == JsonValueKind.Number)
                    {
                        return value.NumberValue.ToString("0.###", CultureInfo.InvariantCulture);
                    }
                }
            }

            return string.Empty;
        }

        private static double? ReadNumber(JsonValue node, params string[] propertyNames)
        {
            if (node == null || propertyNames == null)
            {
                return null;
            }

            if (node.Kind == JsonValueKind.Number)
            {
                return node.NumberValue;
            }

            foreach (var propertyName in propertyNames)
            {
                if (string.IsNullOrWhiteSpace(propertyName) || !node.TryGetPropertyIgnoreCase(propertyName, out var value))
                {
                    continue;
                }

                if (value.Kind == JsonValueKind.Number)
                {
                    return value.NumberValue;
                }

                if (value.Kind == JsonValueKind.String
                    && double.TryParse(value.StringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static int ReadInt(JsonValue node, params string[] propertyNames)
        {
            var number = ReadNumber(node, propertyNames);
            return number.HasValue
                ? Convert.ToInt32(Math.Round(number.Value))
                : 0;
        }

        private static bool? ReadBoolean(JsonValue node, params string[] propertyNames)
        {
            if (node == null || propertyNames == null)
            {
                return null;
            }

            foreach (var propertyName in propertyNames)
            {
                if (string.IsNullOrWhiteSpace(propertyName) || !node.TryGetPropertyIgnoreCase(propertyName, out var value))
                {
                    continue;
                }

                if (value.Kind == JsonValueKind.Boolean)
                {
                    return value.BooleanValue;
                }

                if (value.Kind == JsonValueKind.String)
                {
                    if (bool.TryParse(value.StringValue, out var parsed))
                    {
                        return parsed;
                    }

                    if (double.TryParse(value.StringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
                    {
                        return Math.Abs(numeric) > 0.001d;
                    }
                }
            }

            return null;
        }
    }
}
