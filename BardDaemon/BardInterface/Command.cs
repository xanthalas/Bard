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

using System;
using Newtonsoft.Json;

namespace BardInterface
{
    public class Command
    {
        public string Cmd;
        public String StringArgument;
        public int IntegerArgument;

        public Command(string command, string stringArgument, int integerArgument)
        {
            Cmd = command;
            StringArgument = stringArgument;
            IntegerArgument = integerArgument;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static Command FromString(string initialiser)
        {
            Command bc;
            try
            {
                bc = JsonConvert.DeserializeObject<Command>(initialiser);

            }
            catch (Newtonsoft.Json.JsonException)
            {
                bc = null;
            }
            return bc;
        }
    }
}
