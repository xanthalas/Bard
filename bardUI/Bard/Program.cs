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
using System.IO;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using BardInterface;

namespace Bard
{
    class Program
    {
        private const string REGEX_NUMBERS = @"^\d+$";
        private static string ipAddressString = "";
        private static int port = 0;

        static void Main(string[] args)
        {
            Command command = buildCommand(args);
            
            if (command == null)
            {
                Console.WriteLine("Invalid command. Format is: Bard action <parameter>");
                Console.WriteLine("                 For example: Bard playfile Creep.mp3");
                Console.WriteLine("\n\nAvailable commands are:");

                writeCommands();
            }
            else
            {
                if (command.Cmd.ToLower() == "start")
                {
                    Console.WriteLine("Starting Server");
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("barddaemon.exe");
                    psi.RedirectStandardOutput = false;
                    psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    psi.UseShellExecute = true;
                    System.Diagnostics.Process proc;
                    proc = System.Diagnostics.Process.Start(psi);
                }
                sendCommand(command);
            }
        }

        private static void sendCommand(Command command)
        {
            TcpClient tcpclnt = null;

            try
            {
                getConnectionInfo();

                tcpclnt = new TcpClient();

                tcpclnt.Connect(ipAddressString, port);

                Stream stm = tcpclnt.GetStream();

                ASCIIEncoding asen = new ASCIIEncoding();
                byte[] ba = asen.GetBytes(command.ToString());

                stm.Write(ba, 0, ba.Length);

                byte[] bb = new byte[100000];
                int k = stm.Read(bb, 0, 100000);

                StringBuilder result = new StringBuilder();

                for (int i = 0; i < k; i++)
                {
                    result.Append(Convert.ToChar(bb[i]));
                }
                Response response = Response.FromString(result.ToString());
                result = null;

                if (response == null || response.Status == Response.INVALID_COMMAND)
                {
                    Console.WriteLine($"Invalid command {command.Cmd} sent. Error message: {response?.Message}");
                    return;
                }

                switch (command.Cmd.ToLower())
                {
                    case "getplaylist":
                    case "playlist":
                        printPlaylist(response);
                        break;

                    case "nowplaying":
                    case "playing":
                        printNowPlaying(response);
                        break;

                    default:
                        Console.WriteLine($"Response: {response.Status} : {response.Message}");
                        break;
                }

            }
            catch (System.Net.Sockets.SocketException)
            {
                Console.WriteLine("Please use Bard start to start the server");
                //Console.WriteLine(e.GetType().ToString());
                //Console.WriteLine("An error occurred attempting to connect to the bardDaemon server.");
                //Console.WriteLine(e.Message);
            }
            finally
            {
                tcpclnt.Close();
            }
        }

        private static void getConnectionInfo()
        {
            ipAddressString = ConfigurationManager.AppSettings["ipaddress"];
            if (ipAddressString == null || ipAddressString.Length < 1)
            {
                ipAddressString = "127.0.0.1";
            }

            var portString = ConfigurationManager.AppSettings["port"];
            if (portString == null || portString.Length < 1)
            {
                portString = "8000";
            }

            port = 8000;

            try
            {
                port = Convert.ToInt32(portString);

            }
            catch (Exception)
            {
                //The port will default to 8000
            }
        }

        private static void printNowPlaying(Response response)
        {
            TagLib.File file = null;

            try
            {
               file = TagLib.File.Create(response.Message);
            }
            catch (TagLib.UnsupportedFormatException)
            {
                //Do nothing as we'll handle it below
            }

            if (file != null)
            {
                Console.WriteLine($"Now playing: {(file.Tag.Title != null ? file.Tag.Title : "<untitled>")}  by  {file.Tag.Performers[0]}");
            }
        }

        private static void printPlaylist(Response response)
        {
            var tracknames = new List<string>();
            var artists = new List<string>();
            foreach (var entry in response.Playlist)
            {
                var file = TagLib.File.Create(entry);
                if (file != null)
                {
                    tracknames.Add((file.Tag.Title != null ? file.Tag.Title : "<untitled>"));
                    artists.Add(file.Tag.FirstPerformer);
                }
                else
                {
                    tracknames.Add(Path.GetFileName(entry));
                }
            }

            int maxLength = tracknames.Aggregate("", (max, cur) => max.Length > cur.Length ? max : cur).Length;

            Console.WriteLine($"Playlist contains {response.Playlist.Count} items:");
            int index = 0;
            foreach (var name in tracknames)
            {
                var outName = name.PadRight(maxLength + 2, ' ');
                var outArtist = "";
                if (artists.Count > index)
                {
                    outArtist = artists[index];
                }

                int outIndex = index + 1;
                Console.WriteLine($"{outIndex.ToString().PadLeft(3, ' ')} {outName}  by  {outArtist}");
                index++;
            }
        }

        private static Command buildCommand(string[] args)
        {
            if (args.Length == 0)
            {
                return null;
            }

            Command command = new Command(args[0], String.Empty, 0);

            if (args.Length > 1)
            {
                if (Regex.IsMatch(args[1], REGEX_NUMBERS))
                {
                    command.IntegerArgument = Convert.ToInt32(args[1]);
                }
                else
                {
                    for(int i = 1; i < args.Length; i++)
                    {
                        command.StringArgument += " " + args[i];
                    }
                    
                    if (command.StringArgument.EndsWith("\""))
                    {
                        command.StringArgument = command.StringArgument.TrimEnd(new char[] { '"' });
                    }
                }
            }

            return command;
        }

        private static void writeCommands()
        {
            Console.WriteLine("start                       - Start the Bard server");
            Console.WriteLine("addfolder     <folder>      - Load all mp3/wma/m4a files in the given folder into the playlist");
            Console.WriteLine("addfile       <filename     - Add a single file to the end of the playlist");
            Console.WriteLine("playfolder    <folder>      - Load all mp3/wma/m4a files in the given folder into the playlist and start playing");
            Console.WriteLine("playfile      <filename>    - Load a single file into the playlist and play it");
            Console.WriteLine("clear                       - Stop playback and clear the playlist");
            Console.WriteLine("  clearplaylist ");
            Console.WriteLine("play                        - Resume playing");
            Console.WriteLine("  start");
            Console.WriteLine("playpause                   - Toggle playback");
            Console.WriteLine("  toggle");
            Console.WriteLine("next                        - Jump to the next track in the playlist");
            Console.WriteLine("  nexttrack");
            Console.WriteLine("prev                        - Jump to the previous track in the playlist");
            Console.WriteLine("  previoustrack");
            Console.WriteLine("  prevtrack");
            Console.WriteLine("stop                        - Stop playing (pause)");
            Console.WriteLine("volume <0-100>              - Set playback volume between 0 (off) and 100 (max)");
            Console.WriteLine("  vol <0-100>");
            Console.WriteLine("playing                     - Show which track is currently playing");
            Console.WriteLine("  nowplaying");
            Console.WriteLine("playlist                    - Retrieve details of the current playlist");
            Console.WriteLine("  getplaylist");
            Console.WriteLine("getlogfile                  - Returns the path to the log file");
            Console.WriteLine("quit                        - Stop playing and close the player");

        }


    }
}
