﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace Lithium.Models
{
    public class Perspective
    {
        public class Api
        {
            private readonly string URL;

            public Api(string apikey)
            {
                ApiKey = apikey;
                URL = $"https://commentanalyzer.googleapis.com/v1alpha1/comments:analyze?key={ApiKey}";
            }

            private string ApiKey { get; }

            public AnalyzeCommentResponse SendRequest(AnalyzeCommentRequest request)
            {
                using (var client = new HttpClient())
                {
                    var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8,
                        "application/json");
                    var response = client.PostAsync(URL, content).Result;
                    response.EnsureSuccessStatusCode();
                    var data = response.Content.ReadAsStringAsync().Result;
                    var result = JsonConvert.DeserializeObject<AnalyzeCommentResponse>(data);
                    return result;
                }
            }

            public string GetResponseString(AnalyzeCommentRequest request)
            {
                using (var client = new HttpClient())
                {
                    var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8,
                        "application/json");
                    var response = client.PostAsync(URL, content).Result;
                    response.EnsureSuccessStatusCode();
                    return response.Content.ReadAsStringAsync().Result;
                }
            }


            public AnalyzeCommentResponse QueryToxicity(string input)
            {
                var requestedAttributeses =
                    new Dictionary<string, RequestedAttributes> {{"TOXICITY", new RequestedAttributes()}};
                var req = new AnalyzeCommentRequest(input, requestedAttributeses);
                var res = SendRequest(req);
                return res;
            }
        }


        public class AnalyzeCommentRequest
        {
            public string clientToken;
            public Comment comment;
            public bool doNotStore;
            public string[] languages = {"en"};
            public Dictionary<string, RequestedAttributes> requestedAttributes;

            public AnalyzeCommentRequest(string comment,
                Dictionary<string, RequestedAttributes> requestedAttributeses = null, bool doNotStore = true,
                string clienttoken = null)
            {
                this.comment = new Comment(comment);
                this.doNotStore = doNotStore;
                if (requestedAttributeses == null)
                    requestedAttributes.Add("TOXICITY", new RequestedAttributes());
                else
                    requestedAttributes = requestedAttributeses;
                clientToken = clienttoken;
            }
        }

        public class AnalyzeCommentResponse
        {
            public AttributeScores attributeScores { get; set; }
            public List<string> languages { get; set; }

            public class AttributeScores
            {
                public _TOXICITY TOXICITY { get; set; }

                public class _TOXICITY
                {
                    public List<SpanScore> spanScores { get; set; }
                    public SummaryScore summaryScore { get; set; }

                    public class SpanScore
                    {
                        public int begin { get; set; }
                        public int end { get; set; }
                        public Score score { get; set; }

                        public class Score
                        {
                            public double value { get; set; }
                            public string type { get; set; }
                        }
                    }

                    public class SummaryScore
                    {
                        public double value { get; set; }
                        public string type { get; set; }
                    }
                }
            }
        }

        public class Comment
        {
            public Comment(string text, string type = "PLAIN_TEXT")
            {
                this.text = text;
                this.type = type;
            }

            public string text { get; set; }
            public string type { get; set; }

            public static implicit operator string(Comment v)
            {
                throw new NotImplementedException();
            }
        }

        public class RequestedAttributes
        {
            public float scoreThreshold;
            public string scoreType;

            public RequestedAttributes(string scoretype = "PROBABILITY", float scorethreshold = 0)
            {
                scoreType = scoretype;
                scoreThreshold = scorethreshold;
            }
        }
    }
}