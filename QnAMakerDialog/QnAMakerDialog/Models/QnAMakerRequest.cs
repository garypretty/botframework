using System;
using Newtonsoft.Json;

namespace QnAMakerDialog.Models
{
    [Serializable]
    public class QnAMakerRequest
    {
        [JsonProperty(PropertyName = "question")]
        public string Question { get; set; }

        [JsonProperty(PropertyName = "top")]
        public int Top { get; set; }

        [JsonProperty(PropertyName = "strictFilters")]
        public Metadata[] StrictFilters { get; set; }

        [JsonProperty(PropertyName = "metadataBoost")]
        public Metadata[] MetadataBoost { get; set; }

        [JsonProperty(PropertyName = "userId")]
        public string UserId { get; set; }
    }
}
