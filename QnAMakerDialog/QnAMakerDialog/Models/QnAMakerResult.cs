using System;
using Newtonsoft.Json;

namespace QnAMakerDialog.Models
{
    [Serializable]
    public class QnAMakerResult
    {
        [JsonProperty(PropertyName = "answers")]
        public QnaAnswer[] Answers { get; set; }
    }

    [Serializable]
    public class QnaAnswer
    {
        [JsonProperty(PropertyName = "score")]
        public float Score { get; set; }

        [JsonProperty(PropertyName = "qnaId")]
        public int QnaId { get; set; }

        [JsonProperty(PropertyName = "answer")]
        public string Answer { get; set; }

        [JsonProperty(PropertyName = "source")]
        public string Source { get; set; }

        [JsonProperty(PropertyName = "questions")]
        public string[] Questions { get; set; }

        [JsonProperty(PropertyName = "metadata")]
        public Metadata[] Metadata { get; set; }
    }
}