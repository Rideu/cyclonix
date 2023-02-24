using System.Collections.Generic;

using Cyclonix.Utils;

namespace Cyclonix.Net
{

    /// <summary> Represents data transmission from server back to remote client </summary>
    public struct Transmission
    { 

        private StreamNode _node;
          
        /// <summary> The main payload that is sent after <see cref="Data"/></summary>
        public StreamNode SendStream => _node;

        /// <summary> A collection of <see cref="LongRange"/>s each of which is mapping for its corresponding region inside the <see cref="SendStream"/> data </summary>
        public IEnumerable<LongRange> Ranges { get; set; }
        //public bool Compressed => Response.Compress;

        /// <summary> Defines, whether the transmission should close the corresponding node upon it's sent </summary>
        public bool Close { get; set; }

        /// <summary> Initial data block of the transmission that is sent before the main payload in <see cref="SendStream"/> </summary>
        /// <remarks> Commonly used a header </remarks>
        public byte[] Data { get; internal set; }
         
        public Transmission(StreamNode node)
        {
            _node = node;
        }
    }
}
