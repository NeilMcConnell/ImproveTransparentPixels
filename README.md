# ImproveTransparentPixels
image manipulator - only modify the color information of transparent pixels

The color information in transparent pixels can become important in two key ways - it will affect DXT compression, and colors information can bleed from tranparent pixels when filtering.  Lots of info on this topic here:
https://www.adriancourreges.com/blog/2017/05/09/beware-of-transparent-pixels/


Flaming Pear makes a Photoshop plugin which deals with this very well, but I'm writing a stand-alone app for people who would like to get away from having a photoshop step in their pipeline.  I am using the ImageMagick Magick.Net library to read & write image files.  (ImageMagick plugin is at https://github.com/dlemstra/Magick.NET, although I got my version through NuGet)

Note that this program does not change the transparency information (alpha channel), and it does not alter color information in pixels that are not fully transparent. So if you're just looking at the image after processing, it should look the same as it did before processing.


Parameters:
any paremeter that does not start with a dash (-) will be assumed to be the input file name
-in:filename  -> specifies the input file
-out:filename -> specifies the output file.  If no output file is specified, then the output file will be the input filename with the extension ".ImprovedTransparent.png"
-preview:filename -> specifies a preview file (optional).  This will be the same as the output file, but with all pixels set fully opaque
-solidify[:maxDistance] -> will smear color from visible pixels to transparent pixels.  If you include a maxDistance parameter, then it will only smear up to maxDistance away from a non-transparent pixel.  So if you run with -solidify:4, then it will smear color up to 4 pixels away from any non-transparent pixel.  Vertical and horizontal distances are summed this calculation, so 2 pixels vertical + 2 pixels horizontal is also a distance of 4.  If maxDistance is not specified, then all pixels will be processed.
-color[:colorhex] -> fills transparent pixels with the specified color.  Defaults to black if no color is specified. So, for example, if you wanted to fill all transparent pixels with white, you can use -color:#FFFFFF


Since -solidify and may leave some pixels unprocessed, you can specify another operation to run afterwards.  Operations will run in the order they are specified, and will only affect pixels that were not processed by previous operations.  So, for example, you may want to smear color up to 8 pixels away from non-transparent pixels, but then fill all remaining space with a solid color to reduce file size.  To do this, you could use
ImproveTransparentPixels -in:myImage.png -solidify:8 -color
