using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DynamicODataSampleService.Models
{
    public class ODataQueryResult
    {
        [Description("Count of records in result set")]
        public int Count { get; set; }

        [Description("URL to fetch next set of records. Example : For skip = 0 and top = 10, this will contain link to retrieve 10 records after skipping first 10 records." +
            "\n NextLink will be null if this is the last set of records")]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string NextLink { get; set; }

        [Description("Records in current set")]
        public IEnumerable<dynamic> Value { get; set; }
    }
}
