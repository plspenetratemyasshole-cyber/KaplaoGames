using System;
using UnityEngine;

namespace KaplaoGames.Shared
{
    [Serializable]
    public sealed class SelectionFilterPayload
    {
        public int criterion;
        public bool allowBlock = true;
        public bool allowSpike = true;
        public bool allowTrigger = true;
        public bool allowParticle = true;
        public bool allowKeyframe = true;
        public bool allowExecutor = true;
        public string[] allowedColorsHex = Array.Empty<string>();
        public int[] allowedGroups = Array.Empty<int>();
        public int dateOp;
        public SelectionFilterDate dateThreshold;
        public int numericOp;
        public float numericValue;
        public float numericValueX;
        public float numericValueY;

        public static SelectionFilterPayload FromFilter(CreateModeSelectionFilter filter)
        {
            if (filter == null)
                filter = CreateModeSelectionFilter.CreateDefault();

            return new SelectionFilterPayload
            {
                criterion = (int)filter.criterion,
                allowBlock = filter.allowBlock,
                allowSpike = filter.allowSpike,
                allowTrigger = filter.allowTrigger,
                allowParticle = filter.allowParticle,
                allowKeyframe = filter.allowKeyframe,
                allowExecutor = filter.allowExecutor,
                allowedColorsHex = filter.allowedColorsHex?.ToArray() ?? Array.Empty<string>(),
                allowedGroups = filter.allowedGroups?.ToArray() ?? Array.Empty<int>(),
                dateOp = (int)filter.dateOp,
                dateThreshold = filter.dateThreshold,
                numericOp = (int)filter.numericOp,
                numericValue = filter.numericValue,
                numericValueX = filter.numericValueX,
                numericValueY = filter.numericValueY
            };
        }

        public CreateModeSelectionFilter ToFilter()
        {
            var filter = new CreateModeSelectionFilter
            {
                criterion = (SelectionFilterCriterion)Mathf.Clamp(criterion, 0, (int)SelectionFilterCriterion.Order),
                allowBlock = allowBlock,
                allowSpike = allowSpike,
                allowTrigger = allowTrigger,
                allowParticle = allowParticle,
                allowKeyframe = allowKeyframe,
                allowExecutor = allowExecutor,
                dateOp = (SelectionCompareOp)Mathf.Clamp(dateOp, 0, (int)SelectionCompareOp.NotEqual),
                dateThreshold = dateThreshold,
                numericOp = (SelectionCompareOp)Mathf.Clamp(numericOp, 0, (int)SelectionCompareOp.NotEqual),
                numericValue = numericValue,
                numericValueX = numericValueX,
                numericValueY = numericValueY
            };

            filter.allowedColorsHex = allowedColorsHex != null
                ? new System.Collections.Generic.List<string>(allowedColorsHex)
                : new System.Collections.Generic.List<string>();
            filter.allowedGroups = allowedGroups != null
                ? new System.Collections.Generic.List<int>(allowedGroups)
                : new System.Collections.Generic.List<int>();
            return filter;
        }

        public static string Serialize(CreateModeSelectionFilter filter)
        {
            if (filter == null || filter.IsDefault)
                return string.Empty;

            return JsonUtility.ToJson(FromFilter(filter));
        }

        public static CreateModeSelectionFilter Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return CreateModeSelectionFilter.CreateDefault();

            try
            {
                var payload = JsonUtility.FromJson<SelectionFilterPayload>(json);
                return payload?.ToFilter() ?? CreateModeSelectionFilter.CreateDefault();
            }
            catch
            {
                return CreateModeSelectionFilter.CreateDefault();
            }
        }
    }
}
