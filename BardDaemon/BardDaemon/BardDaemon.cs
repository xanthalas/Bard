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
        private static IWavePlayer waveOutDevice = null;
        private static WaveStream audioFileReader;
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
                    Console.WriteLine("Setting ip address to 127.0.0.1");
                    writeToLog("Setting ip address to 127.0.0.1");
                }
                IPAddress ipAddress = IPAddress.Parse(ipAddressString);
                // You can use local IP as far as you use the same in client

                var portString = ConfigurationManager.AppSettings["port"];
                if (portString == null || portString.Length < 1)
                {
                    Console.WriteLine("Setting port to 8000");
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
                    Console.WriteLine($"Couldn't convert the port. Message {e.Message}");
                    writeToLog($"Couldn't convert the port. Message {e.Message}");
                    //The port will default to 8000
                }
                // Initializes the Listener
                Console.WriteLine($"Trying to connect to {ipAddress.Address} on port {port}");
                writeToLog($"Trying to connect to {ipAddress.Address} on port {port}");
                server = new TcpListener(ipAddress, port);

                // Start Listeneting at the specified port
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
                    //Console.Write("Waiting for a connection... ");

                    // Perform a blocking call to accept requests.
                    // You could also user server.AcceptSocket() here.
                    TcpClient client = server.AcceptTcpClient();
                    //Console.WriteLine("Connected!");

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
                    //Console.WriteLine("Sent: {0}", data);

                    // Shutdown and end connection
                    client.Close();
                }

            }
            catch (SocketException e)
            {
                Console.WriteLine($"SocketException: {e}");
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
            //Console.WriteLine($"Processing command {command.ToString()}");
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
                        Console.WriteLine($"Cannot initialise playback of file {command.StringArgument}");
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
                        Console.WriteLine($"Cannot initialise playback of file {command.StringArgument}");
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
                        Console.WriteLine($"Cannot initialise playback of file {command.StringArgument}");
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
                        Console.WriteLine($"Cannot initialise playback of file {command.StringArgument}");
                    }
                    break;

                case "clear":
                case "clearplaylist":
                    userStopped = true;
                    finishPlayback();
                    playlist.Clear();
                    nowPlayingPlaylistIndex = 0;
                    Console.WriteLine($"Playlist cleared. Now contains {playlist.Count} entries");
                    writeToLog($"Playlist cleared. Now contains {playlist.Count} entries");
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
                        //                        Console.WriteLine($"IntegerArgument is {command.IntegerArgument} so setting volume to {newVolume}");
                        writeToLog($"Setting volume to {newVolume}");
                        if (audioFileReader.GetType() == typeof(Mp3FileReader))
                        {
                            var mp3fr = audioFileReader as AudioFileReader;

                            if (mp3fr != null)
                            {
                                mp3fr.Volume = newVolume;
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

            //Console.WriteLine($"In initialisePlayback. nowPlayingPlaylistIndex = {nowPlayingPlaylistIndex}. Playlist count = {playlist.Count}");
            if (nowPlayingPlaylistIndex < playlist.Count)
            {
                Console.WriteLine($"Playing {playlist[nowPlayingPlaylistIndex]}");
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
                    try
                    {
                        audioFileReader = new WaveFileReader(playlist[nowPlayingPlaylistIndex]);
                        validReaderCreated = true;
                    }
                    catch (System.InvalidOperationException)
                    {
                        writeToLog($"Cannot play file. It is not one of the supported formats or is corrupt. File: {playlist[nowPlayingPlaylistIndex]}");
                        validReaderCreated = false;
                    }
                    catch (System.FormatException)
                    {
                        writeToLog($"Cannot play file. It is not one of the supported formats or is corrupt. File: {playlist[nowPlayingPlaylistIndex]}");
                        validReaderCreated = false;
                    }
                }

                if (!validReaderCreated)
                {
                    playbackInitialised = false;

                }
                else
                {
                    waveOutDevice = null;
                    waveOutDevice = new DirectSoundOut();
                    waveOutDevice.PlaybackStopped += WaveOutDevice_PlaybackStopped;
                    waveOutDevice.Init(audioFileReader);
                    nowPlayingTrack = playlist[nowPlayingPlaylistIndex];
                    playbackInitialised = true;
                }
            }
            else
            {
                Console.WriteLine("End of playlist reached");
                writeToLog("End of playlist reached");
                nowPlayingPlaylistIndex = 0;
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
            //Console.WriteLine("Playing file");
            if (playbackInitialised && !currentlyPlaying)
            {
                waveOutDevice.Play();
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
                waveOutDevice.Stop();
            }

            currentlyPlaying = false;

            response.Status = Response.OK;
            response.Message = "Playback stopped";
            writeToLog("Playback stopped");
        }

        private static void finishPlayback()
        {
            //Console.WriteLine("Finishing playback");
            if (waveOutDevice != null)
            {
                waveOutDevice.Stop();
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
