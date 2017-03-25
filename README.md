# Bard
Bard is a console-based music player for Windows. It has no user interface and runs as a server which you interact with using the command line. 

## Usage

To start the server issue the command 

    bard start

Once the server is running you can tell it to play all the music files in a given folder with the command:

    bard playfolder folder-to-play

For example entering  bard playfolder 'C:\music\placebo\Loud Like Love' will add all the files from the folder to the playlist and begin playing the first track.

To see which song is currently playing issue the command:

    bard nowplaying

To see the songs in the current playlist issue the command:

    bard playlist

To toggle playback issue the command:

    bard playpause

Finally to stop the server issue the command:

    bard quit

There are plenty of other commands for skipping tracks, changing volume, etc. To discover them type:

    bard --help

## Configuration

The only thing you need to configure is the port on which the bard server will listen for commands. This is done in the bard.exe.config file. Simply change the "port" value to the port number you wish to use. By default it will listen on port 8000.

## Installation

Simply unzip the downloaded file into an empty directory and ensure that the directory is on your path.