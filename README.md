# String2JSON
A program to convert Marvel Heroes .string files to json files
and convert them back to .string files

Marvel Heroes stores all of the in-game text in .string files
for different languages.  These files are difficult to edit as
their .string format

Converting to JSON allows you to edit the text in the JSON file
in any text editor without knowing the .string format

When your edits are complete, String2JSON will convert your 
new JSON back into a .string file

The precompiled version is in String2JSON.zip, 
download and unzip to any folder

To use just drap-and-drop a .string or .json file onto 
String2JSON.exe and it will be converted

Marvel Heroes .string files are in Marvel Heroes\Data\Game\Loco 
with folders for each language

Example:

String2JSON.exe my.string
- will create a JSON file my.string.json

String2JSON.exe my.string.json
- will create a .string file my.string.json.string
