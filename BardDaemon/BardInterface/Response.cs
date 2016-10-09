/* Copyright (c) 2016 xanthalas
 * 
 * Author: Xanthalas
 * Date  : September 2016
 * 
 *  This file is part of Bard.
 *
 *  Bard is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Bard is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Bard.  If not, see <http://www.gnu.org/licenses/>.
 */

using System.Collections.Generic;
using Newtonsoft.Json;

namespace BardInterface
{
    public class Response
    {
        public const string OK = "OK";
        public const string INVALID_COMMAND = "INVALID COMMAND";

        public string Status;
        public string Message;
        public List<string> Playlist;

        public Response()
        {
             Playlist = new List<string>();
        }

        public Response(string status)
            :this()
        {
            Status = status;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static Response FromString(string initialiser)
        {
            Response resp;
            try
            {
                resp = JsonConvert.DeserializeObject<Response>(initialiser);

            }
            catch (Newtonsoft.Json.JsonException)
            {
                resp = null;
            }
            return resp;
        }
    }
}
