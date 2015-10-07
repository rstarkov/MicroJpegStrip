# MicroJpegStrip
This program strips all metadata from a JPEG file without re-encoding the image data. This includes thumbnails, EXIF info, color profiles etc. By default, this program will create a new file for each input file, adding '.stripped' before the extension. If the destination file already exists, a number will be appended to the output file name to make it unique.

Note: this is a command-line program. There is no UI.

**Usage:** MicroJpegStrip.exe [-o] jpeg1 [jpeg2 [...]]

Specify -o (overwrite) as the first parameter to overwrite each input file instead. Use this option at your own risk!

Returns 0 if all files were processed successfully, otherwise returns the number of files with errors.

**DISCLAIMER:** this program has only been tested on a small number of JPEG files. It may produce an empty or broken output for some inputs and work for others. Don't use the -o option on files you can't risk losing! Don't assume that just because it worked on one file, you can safely use it on others. Finally, I make no guarantees that this program will actually remove any personal information you might be trying to strip from your JPEGs.
