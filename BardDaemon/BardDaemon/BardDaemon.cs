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
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NAudio;
using System.Net;
using System.Net.Sockets;
using NAudio.Wave;
using BardInterface;

namespace BardDaemon
{
    public class BardDaemon
    {
        private static IWavePlayer wavePlayer = null;
        private static AudioFileReader audioFileReader;
        private static bool playbackInitialised = false;

        /// <summary>
        /// Flag indicating whether playback is stopping because the user selected Stop.
        /// </summary>
        private static bool userStopped = false;
        private static List<string> playlist = new List<string>();
        private static int nowPlayingPlaylistIndex = 0;
        private static bool currentlyPlaying = false;
        private static Response response = new Response();
        private static string nowPlayingTrack = String.Empty;
        private static string[] VALIDFORMATS = {".mp3", ".wma", ".m4a", ".wav"};
        private static readonly string logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BardDaemon.log");

        public static void Main(string[] args)
        {
            writeToLog("------------------------------ Server starting ------------------------------");
            TcpListener server = null;

            try
            {
                var ipAddressString = ConfigurationManager.AppSettings["ipaddress"];
                if (ipAddressString == null || ipAddressString.Length < 1)
                {
                    ipAddressString = "127.0.0.1";
                    writeToLog("Setting ip address to 127.0.0.1");
                }
                IPAddress ipAddress = IPAddress.Parse(ipAddressString);
                // You can use local IP as far as you use the same in client

                var portString = ConfigurationManager.AppSettings["port"];
                if (portString == null || portString.Length < 1)
                {
                    writeToLog("Setting port to 8000");
                    portString = "8000";
                }

                int port = 8000;

                try
                {
                    port = Convert.ToInt32(portString);

                }
                catch (Exception e)
                {
                    writeToLog($"Couldn't convert the port. Message {e.Message}");
                    //The port will default to 8000
                }
                // Initializes the Listener
                writeToLog($"Trying to connect to {ipAddress.Address} on port {port}");
                server = new TcpListener(ipAddress, port);

                // Start Listening at the specified port
                server.Start();

                Console.WriteLine($"Server running on address {ipAddressString} Port: {port}");
                Console.WriteLine("Local end point:" + server.LocalEndpoint);
                Console.WriteLine("Waiting for connections...");

                writeToLog($"Server running on address {ipAddressString} Port: {port}");
                writeToLog("Local end point:" + server.LocalEndpoint);
                
                // Buffer for reading data
                Byte[] bytes = new Byte[512];
                String data = null;
                Command command = null;
                bool quit = false;

                // Enter the listening loop.
                while (!quit)
                {
                    // Perform a blocking call to accept requests.
                    TcpClient client = server.AcceptTcpClient();

                    data = null;

                    // Get a stream object for reading and writing
                    NetworkStream stream = client.GetStream();

                    int i;


                    i = stream.Read(bytes, 0, bytes.Length);
                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);

                    command = Command.FromString(data);

                    if (command == null)
                    {
                        response = new Response(Response.INVALID_COMMAND);
                        response.Message = "Data passed: " + data;
                    }
                    else
                    {
                        response = new Response(Response.OK);

                        if (command.Cmd == "quit")
                        {
                            quit = true;
                        }
                        else
                        {
                            processCommand(command);
                        }
                    }

                    byte[] msg = System.Text.Encoding.ASCII.GetBytes(response.ToString());

                    // Send back a response.
                    stream.Write(msg, 0, msg.Length);

                    // Shutdown and end connection
                    client.Close();
                }

            }
            catch (SocketException e)
            {
                writeToLog($"SocketException: {e}");
            }
            finally
            {
                writeToLog("Stopping server");
                server.Stop();
            }
        }

        private static void processCommand(Command command)
        {
            if (command == null)
            {
                return;
            }
            switch (command.Cmd.ToLower())
            {
                case "playfile":
                    if (File.Exists(command.StringArgument))
                    {
                        playlist.Clear();
                        nowPlayingPlaylistIndex = 0;
                        playlist.Add(command.StringArgument);
                        initialisePlayback(playlist);
                        if(play())
                        {
                            response.Status = Response.OK;
                            response.Message = $"Playing {nowPlayingTrack}";
                            writeToLog(response.Message);
                        }
                    }
                    else
                    {
                        writeToLog($"Cannot initialise playback of file {command.StringArgument}");
                    }
                    break;

                case "addfile":
                    if (File.Exists(command.StringArgument))
                    {
                        playlist.Add(command.StringArgument);
                        response.Status = Response.OK;
                        response.Message = $"1 file added to playlist. Playlist contains {playlist.Count} entries.";
                        writeToLog(response.Message);
                    }
                    else
                    {
                        writeToLog($"Cannot initialise playback of file {command.StringArgument}");
                    }
                    break;

                case "addfolder":
                    if (Directory.Exists(command.StringArgument))
                    {
                        addFolderToPlaylist(command.StringArgument);
                    }
                    else
                    {
                        writeToLog($"Cannot initialise playback of file {command.StringArgument}");
                    }
                    break;

                case "playfolder":
                    if (Directory.Exists(command.StringArgument))
                    {
                        playlist.Clear();
                        nowPlayingPlaylistIndex = 0;
                        addFolderToPlaylist(command.StringArgument);
                        initialisePlayback(playlist);
                        play();
                    }
                    else
                    {
                        writeToLog($"Cannot initialise playback of file {command.StringArgument}");
                    }
                    break;

                case "clear":
                case "clearplaylist":
                    userStopped = true;
                    finishPlayback();
                    playlist.Clear();
                    nowPlayingPlaylistIndex = 0;
                    writeToLog($"Playlist cleared. Now contains {playlist.Count} entries");
                    userStopped = false;
                    break;

                case "play":
                case "start":
                    if (command.IntegerArgument > 0)
                    {
                        if (command.IntegerArgument < playlist.Count)
                        {
                            userStopped = true;
                            finishPlayback();
                            nowPlayingPlaylistIndex = command.IntegerArgument - 1;
                            if (initialisePlayback(playlist))
                            {
                                play();
                                response.Status = Response.OK;
                                response.Message = $"Playing track {nowPlayingPlaylistIndex + 1}: {Path.GetFileName(nowPlayingTrack)}";
                                writeToLog(response.Message);
                            }
                        }
                    }
                    if (playbackInitialised)
                    {
                        play();
                    }
                    else
                    {
                        if(initialisePlayback(playlist))
                        {
                            play();
                        }
                    }
                    break;

                case "playpause":
                case "toggle":
                    if (currentlyPlaying)
                    {
                        userStopped = true;
                        stop();
                    }
                    else
                    {
                        play();
                    }
                    break;

                case "next":
                case "nexttrack":
                    userStopped = true;
                    finishPlayback();
                    nowPlayingPlaylistIndex++;
                    if (initialisePlayback(playlist))
                    {
                        play();
                    }
                    break;

                case "prev":
                case "previoustrack":
                case "prevtrack":
                    userStopped = true;
                    finishPlayback();
                    nowPlayingPlaylistIndex--;
                    if (nowPlayingPlaylistIndex < 0)
                    {
                        nowPlayingPlaylistIndex = 0;
                    }
                    if (initialisePlayback(playlist))
                    {
                        play();
                    }
                    break;

                case "stop":
                    if (playbackInitialised)
                    {
                        userStopped = true;
                        stop();
                    }
                    break;

                case "stopaftercurrent":
                    if (playbackInitialised)
                    {
                        userStopped = true;
                    }
                    break;
                case "volume":
                case "vol":
                    if (command.IntegerArgument >= 0 && command.IntegerArgument <= 100)
                    {
                        float newVolume = (float)command.IntegerArgument / 100f;
                        writeToLog($"Setting volume to {newVolume}");

                        try
                        {
                            audioFileReader.Volume = newVolume;
                        }
                        catch (Exception e)
                        {
                            writeToLog("An exception occurred while attempting to change the volume. The exception text is:");
                            writeToLog(e.Message);
                            if (e.InnerException != null)
                            {
                                writeToLog($"Inner exception: {e.InnerException.Message}");
                            }
                        }                            
                    }
                    break;

                case "playing":
                case "nowplaying":
                    response.Status = Response.OK;
                    if (currentlyPlaying)
                    {
                        response.Message = $"{playlist[nowPlayingPlaylistIndex]}";
                    }
                    else
                    {
                        response.Message = $"Nothing currently playing. Playlist contains {playlist.Count} items";
                    }
                    break;

                case "getplaylist":
                case "playlist":
                    response.Status = Response.OK;
                    response.Message = "";
                    response.Playlist = playlist;
                    writeToLog("Returning playlist information");
                    break;

                case "getlogfile":
                    response.Status = Response.OK;
                    response.Message = logFile;
                    response.Playlist = null;
                    break;

                case "quit":

                    
                default:
                    response.Status = Response.INVALID_COMMAND;
                    response.Message = command.Cmd;
                    writeToLog($"Invalid command received: {command.Cmd}");
                    break;
            }

            command.Cmd = "PROCESSED";
        }

        private static void addFolderToPlaylist(string stringArgument)
        {
            int countOfAddedFiles = 0;
            var files = Directory.GetFiles(stringArgument);

            foreach(var file in files)
            {
                if (VALIDFORMATS.Contains(Path.GetExtension(file).ToLower()))
                {
                    playlist.Add(file);
                    countOfAddedFiles++;
                }
            }

            response.Status = Response.OK;
            response.Message =  $"{countOfAddedFiles} tracks added to playlist";
            writeToLog($"Added {countOfAddedFiles} files to playlist from folder {stringArgument}");
        }

        private static bool initialisePlayback(List<string> playlist)
        {
            playbackInitialised = false;

            if (nowPlayingPlaylistIndex < playlist.Count)
            {
                writeToLog($"Initialising playback of {playlist[nowPlayingPlaylistIndex]}");
                bool validReaderCreated = false;

                try
                {
                    audioFileReader = new AudioFileReader(playlist[nowPlayingPlaylistIndex]);
                    validReaderCreated = true;
                }
                catch (System.InvalidOperationException)
                {
                    validReaderCreated = false;
                }


                if (!validReaderCreated)
                {
                    playbackInitialised = false;

                }
                else
                {
                    wavePlayer = null;
                    wavePlayer = new DirectSoundOut();
                    wavePlayer.PlaybackStopped += WaveOutDevice_PlaybackStopped;
                    wavePlayer.Init(audioFileReader);
                    nowPlayingTrack = playlist[nowPlayingPlaylistIndex];
                    playbackInitialised = true;
                }
            }
            else
            {
                writeToLog("End of playlist reached");
                nowPlayingPlaylistIndex = 0;
                userStopped = false;
                currentlyPlaying = false;
            }

            return playbackInitialised;
        }

        private static void WaveOutDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            writeToLog($"WaveOutDevice_PlaybackStopped event fired. userStopped = {userStopped}");

            if (!userStopped)
            {
                finishPlayback();
                nowPlayingPlaylistIndex++;
                if (initialisePlayback(playlist))
                {
                    play();
                }
            }
            userStopped = false;
        }

        private static bool play()
        {
            if (playbackInitialised && !currentlyPlaying)
            {
                wavePlayer.Play();
                currentlyPlaying = true;
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void stop()
        {
            if (playbackInitialised)
            {
                wavePlayer.Stop();
            }

            currentlyPlaying = false;

            response.Status = Response.OK;
            response.Message = "Playback stopped";
            writeToLog("Playback stopped");
        }

        private static void finishPlayback()
        {
            if (wavePlayer != null)
            {
                wavePlayer.Stop();
            }

            if (audioFileReader != null)
            {
                audioFileReader.Close();
                audioFileReader = null;
            }
            nowPlayingTrack = string.Empty;
            currentlyPlaying = false;
        }

        private static void writeToLog(string message)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(logFile, true))
                {
                    sw.WriteLine($"{DateTime.Now.ToString()}:{message}");
                }
            }
            catch(Exception)
            {
                //Do nothing. If we can't write to the log then there's not a lot else we can do
            }
        }
    }
}
