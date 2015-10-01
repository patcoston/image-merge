using System;
using System.IO;
using System.Drawing;
using ImageMagick;

namespace ImageMerge {
	class Program {
		static MagickImage[] image = new MagickImage[50]; // array of images
		static PixelCollection[] imagePC = new PixelCollection[50]; // array of pixel collections
		static PixelCollection canvasPC; // canvas pixel collections
		static bool[] Placed = new bool[50]; // has the image been placed on the canvas
		static int activeCanvasX1 = 0, activeCanvasY1 = 0, activeCanvasX2 = 0, activeCanvasY2 = 0; // top/left and bottom/right corner of canvas used so far
		static int lastImageX1 = 0, lastImageY1 = 0, lastImageX2 = 0, lastImageY2 = 0; // top/left and bottom/right corner of last image added to canvas
		static int searchCanvasX1 = 0, searchCanvasY1 = 0, searchCanvasX2 = 0, searchCanvasY2 = 0; // top/left and bottom/right corner of area within canvas to search
		static int canvasOverlapX1 = 0, canvasOverlapY1 = 0, canvasOverlapX2 = 0, canvasOverlapY2 = 0; // canvas overlap area
		static int imageOverlapX = 0, imageOverlapY = 0; // image overlap X/Y
		static int maxX1 = 0, maxY1 = 0, maxX2 = 0, maxY2 = 0; // top/left and bottom/right corner of last image added to canvas
		static int overlapMax = 0; // init to zero for current image.  Use when nearness = 0
		static double distanceMinAverage = 0; // minimum distance found so far. Use when nearness > 0
		static int canvasSizeX = 0, canvasSizeY = 0;
		static int canvasX, canvasY; // bottom right corner of canvas
		static int requiredOverlap = 0; // number of pixels required to match on overlap
		static int accuracy = 1; // 1 is most accurate
		static int shortCut = 0; // 0=search canvas 1=search last image added
		static int nearness = 0; // 0=colors must match exactly.  Greater than 0 means find distance between pixel colors.  If it exceeds nearness then skip location. 444 will not skip any.
		static bool[,] UsedPixel;
		static ushort[, ,] imageR; // image RGB array
		static ushort[, ,] imageG;
		static ushort[, ,] imageB;
		static ushort[,] canvasR; // canvas RGB array
		static ushort[,] canvasG;
		static ushort[,] canvasB;
		static ushort canvasBackR = 0; // canvas background Red
		static ushort canvasBackG = 0; // canvas background Green
		static ushort canvasBackB = 0; // canvas background Blue
		static double overlapPercent = 20; // percentage overlap required for image placement when using nearness=1.  20% is default.
		// given overlap areas on canvas, return number of used pixels
		// Some pixels in the overlapped area may not be used yet.
		static int getUsedPixelsCount(int x1, int y1, int x2, int y2) {
			int count = 0;
			for (int y = y1; y <= y2; y += accuracy) {
				for (int x = x1; x <= x2; x += accuracy) {
					if (UsedPixel[x, y])
						count++;
				}
			}
			return count;
		}
		static void setUsedPixels(int x1, int y1, int x2, int y2) {
			for (int y = y1; y <= y2; y++) {
				for (int x = x1; x <= x2; x++) {
					UsedPixel[x, y] = true;
				}
			}
		}
		static void initOverlap(int w, int h, int x, int y) {
			canvasOverlapX1 = canvasOverlapY1 = canvasOverlapX2 = canvasOverlapY2 = 0; // canvas overlap area
			imageOverlapX = imageOverlapY = 0; // image overlap X/Y
			// Calculate canvas overlap X/Y 1/2 - where to start and end on canvas for overlapped pixels with image
			if (x < searchCanvasX1)
				canvasOverlapX1 = searchCanvasX1;
			else // Note: x can never be > canvasOverlapX2
				canvasOverlapX1 = x;
			if (y < searchCanvasY1)
				canvasOverlapY1 = searchCanvasY1;
			else // Note: y can never be > canvasOverlapY2
				canvasOverlapY1 = y;
			canvasOverlapX2 = x + w - 1;
			canvasOverlapY2 = y + h - 1;
			if (canvasOverlapX2 > searchCanvasX2)
				canvasOverlapX2 = searchCanvasX2;
			if (canvasOverlapY2 > searchCanvasY2)
				canvasOverlapY2 = searchCanvasY2;
			requiredOverlap = getUsedPixelsCount(canvasOverlapX1, canvasOverlapY1, canvasOverlapX2, canvasOverlapY2); // required number of pixels to match on overlap
			// Calculate imageOverlap X/Y - where to start inside image for overlapped pixels with canvas
			int canvasOverlapSizeX = canvasOverlapX2 - canvasOverlapX1 + 1;
			int canvasOverlapSizeY = canvasOverlapY2 - canvasOverlapY1 + 1;
			if (x < searchCanvasX1)
				imageOverlapX = w - canvasOverlapSizeX;
			else
				imageOverlapX = 0;
			if (y < searchCanvasY1)
				imageOverlapY = h - canvasOverlapSizeY;
			else
				imageOverlapY = 0;
		}
		static double getAverageDistances(int i, int w, int h, int x, int y, double pixelCountMin) {
			double sum = 0;
			double count = 0;
			double average = 0;
			int x3 = x + w - 1;
			int y3 = y + h - 1;
			for (int y1 = y, y2 = 0; y1 <= y3; y1 += accuracy, y2 += accuracy) {
				for (int x1 = x, x2 = 0; x1 <= x3; x1 += accuracy, x2 += accuracy) {
					ushort r1 = canvasR[x1, y1];
					ushort g1 = canvasG[x1, y1];
					ushort b1 = canvasB[x1, y1];
					ushort r2 = imageR[i, x2, y2];
					ushort g2 = imageG[i, x2, y2];
					ushort b2 = imageB[i, x2, y2];
					// if neither canvas nor image pixel is equal to background color
					if (UsedPixel[x1, y1]) {
						double r3 = Math.Abs(r1 - r2);
						double g3 = Math.Abs(g1 - g2);
						double b3 = Math.Abs(b1 - b2);
						double r4 = r3 * r3 * r3;
						double g4 = g3 * g3 * g3;
						double b4 = b3 * b3 * b3;
						sum += r4 + g4 + b4;
						count++;
						//if ((x == 128) && (y == 648)) {
						//	Console.WriteLine(" x1=" + x1.ToString() + " y1=" + y1.ToString() + " x2=" + x2.ToString() + " y2=" + y2.ToString() + " r3=" + r3.ToString() + " g3=" + g3.ToString() + " b3=" + b3.ToString() + " r4=" + r4.ToString() + " g4=" + g4.ToString() + " b4=" + b4.ToString() + " sum=" + sum.ToString() + " count=" + count.ToString());
						//}
					}
				}
			}
			if (count >= pixelCountMin) // only consider results where count > minimum pixel count
				average = sum / count;
			else
				average = Double.MaxValue; // ignore results where count <= 100
			return average;
		}
		static int getMaxOverlapPerImage(int i, int w, int h, int x, int y) {
			initOverlap(w, h, x, y);
			int overlap = 0;
			for (int y1 = canvasOverlapY1, y2 = imageOverlapY; y1 <= canvasOverlapY2; y1 += accuracy, y2 += accuracy) {
				for (int x1 = canvasOverlapX1, x2 = imageOverlapX; x1 <= canvasOverlapX2; x1 += accuracy, x2 += accuracy) {
					if (UsedPixel[x1, y1]) {
						ushort r1 = canvasR[x1, y1];
						ushort r2 = imageR[i, x2, y2];
						if (r1 != r2)
							return 0;
						ushort g1 = canvasG[x1, y1];
						ushort g2 = imageG[i, x2, y2];
						if (g1 != g2)
							return 0;
						ushort b1 = canvasB[x1, y1];
						ushort b2 = imageB[i, x2, y2];
						if (b1 != b2)
							return 0;
						overlap++;
					}
				}
			}
			return overlap;
		}
		static void findCorrectOverlap(int i) {
			int w = image[i].Width;
			int h = image[i].Height;
			int x1, y1, x2, y2, x3, y3;
			// if using short-cut then iterate through the pixels of the last image added
			if (shortCut == 1) {
				x1 = lastImageX1 - w + 1;
				y1 = lastImageY1 - h + 1;
				x2 = lastImageX2;
				y2 = lastImageY2;
			} else { // otherwise iterate through the pixels of the active canvas
				x1 = activeCanvasX1 - w + 1;
				y1 = activeCanvasY1 - h + 1;
				x2 = activeCanvasX2;
				y2 = activeCanvasY2;
			}
			// calculate bottom right of search area
			x3 = x2 + w - 1;
			y3 = y2 + h - 1;
			// prevent image from being placed outside canvas
			if (x1 < 0)
				x1 = 0;
			if (y1 < 0)
				y1 = 0;
			if (x3 > canvasX)
				x2 = canvasX - w + 1;
			if (y3 > canvasY)
				y2 = canvasY - h + 1;
			// define search area in image based on short cut mode
			if (shortCut == 1) {
				searchCanvasX1 = lastImageX1;
				searchCanvasY1 = lastImageY1;
				searchCanvasX2 = lastImageX2;
				searchCanvasY2 = lastImageY2;
			} else {
				searchCanvasX1 = activeCanvasX1;
				searchCanvasY1 = activeCanvasY1;
				searchCanvasX2 = activeCanvasX2;
				searchCanvasY2 = activeCanvasY2;
			}
			// top/left and bottom/right corners of overlap between active-canvas and image
			if (nearness == 0) {
				overlapMax = 0; // init to zero for current image
				for (int y = y1; y <= y2; y++) {
					for (int x = x1; x <= x2; x++) {
						int overlap = getMaxOverlapPerImage(i, w, h, x, y);
						// if overlap count is greater than maximum found so far and overlap count is equal to required overlap.  All pixels in overlap must match.
						if ((overlap > overlapMax) && (overlap == requiredOverlap)) {
							overlapMax = overlap;
							maxX1 = x;
							maxY1 = y;
						}
					}
				}
			} else {
				distanceMinAverage = Double.MaxValue; // init to number larger than max
				double pixelCountMin = image[i].Width * image[i].Height * overlapPercent; // minimum pixel count for overlap
				//Console.WriteLine("pixelCountMin = " + pixelCountMin.ToString());
				//Console.WriteLine(image[i].Width.ToString() + " x " + image[i].Height.ToString() + " x " + overlapPercent.ToString() + " = " + pixelCountMin.ToString());
				for (int y = y1; y <= y2; y++) {
					for (int x = x1; x <= x2; x++) {
						double distanceAv = getAverageDistances(i, w, h, x, y, pixelCountMin);
						//if ((x == 128) && (y == 648))
						//	Console.WriteLine("x=128 y=648 distanceAv = " + distanceAv.ToString());
						// if average distance less than minimum found so far
						if (distanceAv < distanceMinAverage) {
							distanceMinAverage = distanceAv;
							maxX1 = x;
							maxY1 = y;
							if (distanceAv == 0) { // perfect fit! No need to look further
								Console.Write(" Dist:" + distanceMinAverage.ToString());
								return;
							}
						}
					}
				}
				Console.Write(" Dist:" + distanceMinAverage.ToString());
			}
		}
		static void getCanvasRGB() {
			for (int y = 0; y < canvasSizeY; y++) {
				for (int x = 0; x < canvasSizeX; x++) {
					Pixel p = canvasPC.GetPixel(x, y); // canvas pixel
					canvasR[x, y] = p.GetChannel(0);
					canvasG[x, y] = p.GetChannel(1);
					canvasB[x, y] = p.GetChannel(2);
				}
			}
		}
		static void displaySyntax() {
			Console.WriteLine("Syntax: imagemerge [Size] [Start] [Canvas Color] [Accuracy] [Shortcut] [Nearness]");
			Console.WriteLine("Size: [Width] [Height]");
			Console.WriteLine("  Width of canvas");
			Console.WriteLine("  Height of canvas");
			Console.WriteLine("Start: [Left] [Top]");
			Console.WriteLine("  Left - Starting column of first image in canvas");
			Console.WriteLine("  Top - Starting row of first image in canvas");
			Console.WriteLine("Canvas Color - Background color of canvas. Can be name like none, white, black, red, blue, green, yellow, etc or");
			Console.WriteLine("               like #FF0000, #123456, #ABCDEF, or #DDDDDD");
			Console.WriteLine("Accuracy - How accurate should it try to match pixels?  1 is most accurate.  10 will run faster. 100 even faster.");
			Console.WriteLine("Shortcut - 0 = Compare current to entire canvas 1 = Compare current to last image added to canvas");
			Console.WriteLine("Nearness - 0 = Exact Pixel Match.  1 = Consider all compared pixel pairs in search for closest match. Use with Shortcut=1");
			Console.WriteLine("Overlap - 0 = Ignore. 1-99 specifies percentage overlap. Used with Shortcut=1 and Nearness=1.");
			Console.WriteLine("           Requires shortcut = 1 because it always find a match with the next image.");
			Console.WriteLine("Example: imagemerge 5000 4000 2500 2000 red 20 1 150");
		}
		static void Main(string[] args) {
			if (args.Length != 9) {
				Console.WriteLine("ImageMerge: Version 1.0");
				displaySyntax();
			} else {
				for (int i = 0; i < 50; i++)
					Placed[i] = false;
				canvasSizeX = Convert.ToInt32(args[0]);
				canvasSizeY = Convert.ToInt32(args[1]);
				int canvasStartX = Convert.ToInt32(args[2]);
				int canvasStartY = Convert.ToInt32(args[3]);
				string bgColor = "xc:" + args[4]; // canvas color name or hex color
				accuracy = Convert.ToInt32(args[5]);
				shortCut = Convert.ToInt32(args[6]);
				nearness = Convert.ToInt32(args[7]);
				double overlap = Convert.ToDouble(args[8]);
				Console.WriteLine("\nCanvas Size: " + canvasSizeX.ToString() + "x" + canvasSizeY.ToString());
				Console.WriteLine("Canvas Start: " + canvasStartX.ToString() + "x" + canvasStartY.ToString());
				Console.WriteLine("Background color: " + args[4]);
				Console.WriteLine("Accuracy: " + accuracy.ToString());
				Console.WriteLine("Nearness: " + nearness.ToString());
				Console.WriteLine("Overlap: " + overlap.ToString() + "\n");
				if (overlap != 0) // 0 means use default 20 which is 20% overlap for nearness=1
					overlapPercent = overlap;
				overlapPercent = overlapPercent / 100 / accuracy / accuracy;
				if (!((shortCut == 0) || (shortCut == 1))) {
					Console.WriteLine("shortCut value is " + shortCut.ToString() + ". It must be 0 or 1. Setting to 0.\n\n");
					displaySyntax();
					Console.WriteLine("\n\n");
					shortCut = 0;
				}
				if (nearness < 0) {
					Console.WriteLine("nearness value is " + nearness.ToString() + ". It must be greater than 0. Setting to 0.\n\n");
					displaySyntax();
					Console.WriteLine("\n\n");
					nearness = 0;
				}
				if ((nearness > 0) && (shortCut != 1)) {
					Console.WriteLine("If nearness equal 1 then shortcut must equal 1.  Setting shortcut = 1.\n\n");
					displaySyntax();
					Console.WriteLine("\n\n");
					shortCut = 1;
				}
				UsedPixel = new bool[canvasSizeX, canvasSizeY]; // remembers which pixels are used in the canvas
				canvasR = new ushort[canvasSizeX, canvasSizeY];
				canvasG = new ushort[canvasSizeX, canvasSizeY];
				canvasB = new ushort[canvasSizeX, canvasSizeY];
				for (int y = 0; y < canvasSizeY; y++)
					for (int x = 0; x < canvasSizeX; x++) {
						UsedPixel[x, y] = false;
				}
				using (MagickImage canvas = new MagickImage(bgColor, canvasSizeX, canvasSizeY)) {
					string[] files = Directory.GetFiles(".", "*.png", SearchOption.TopDirectoryOnly);
					int maxWidth = 0, maxHeight = 0;
					int len = files.Length;
					if (len < 2) {
						Console.WriteLine("Need at least 2 images to run.");
						return;
					}
					for (int i = 0; i < len; i++) {
						image[i] = new MagickImage(files[i]);
						imagePC[i] = image[i].GetReadOnlyPixels();
						if (image[i].Width > maxWidth)
							maxWidth = image[i].Width;
						if (image[i].Height > maxHeight)
							maxHeight = image[i].Height;
					}
					// record RGB pixel values for each image to reduce calls to GetPixel() to improve performance
					imageR = new ushort[len, maxWidth, maxHeight];
					imageG = new ushort[len, maxWidth, maxHeight];
					imageB = new ushort[len, maxWidth, maxHeight];
					for (int i = 1; i < len; i++) {
						for (int y = 0; y < image[i].Height; y++) {
							for (int x = 0; x < image[i].Width; x++) {
								Pixel p = imagePC[i].GetPixel(x, y); // image pixel
								imageR[i, x, y] = p.GetChannel(0);
								imageG[i, x, y] = p.GetChannel(1);
								imageB[i, x, y] = p.GetChannel(2);
							}
						}
					}
					canvas.Composite(image[0], new MagickGeometry(canvasStartX, canvasStartY, image[0].Width, image[0].Height), CompositeOperator.Over);
					canvas.Write(@"output\output-1.png");
					canvasPC = canvas.GetReadOnlyPixels();
					getCanvasRGB();
					Placed[0] = true; // first image has been placed on canvas
					// top/left of active-canvas
					activeCanvasX1 = lastImageX1 = canvasStartX;
					activeCanvasY1 = lastImageY1 = canvasStartY;
					// bottom/right of active-canvas
					activeCanvasX2 = lastImageX2 = canvasStartX + image[0].Width - 1;
					activeCanvasY2 = lastImageY2 = canvasStartY + image[0].Height - 1;
					setUsedPixels(activeCanvasX1, activeCanvasY1, activeCanvasX2, activeCanvasY2);
					int outputImageNum = 2;
					bool imagePlaced = false;
					canvasX = canvasSizeX - 1; // right column of pixels
					canvasY = canvasSizeY - 1; // bottom row of pixels
					Console.WriteLine(files[0] + " -> output-1: " + canvasStartX.ToString() + "x" + canvasStartY.ToString());
					do {
						imagePlaced = false;
						for (int i = 1; i < len; i++) {
							if (!Placed[i]) { // if image has not been placed on canvas
								Console.Write(files[i]);
								findCorrectOverlap(i);
								if ((nearness == 0) && (overlapMax == 0))
									Console.WriteLine(": NO MERGE");
								else {
									Console.WriteLine(" -> output-" + outputImageNum.ToString() + ": " + maxX1.ToString() + "x" + maxY1.ToString());
									int w = image[i].Width;
									int h = image[i].Height;
									canvas.Composite(image[i], new MagickGeometry(maxX1, maxY1, w, h), CompositeOperator.Over);
									canvas.Write(@"output\output-" + (outputImageNum++).ToString() + ".png");
									canvasPC = canvas.GetReadOnlyPixels(); // update canvas pixels
									getCanvasRGB();
									// record canvas background for comparison later
									canvasBackR = canvasR[0, 0];
									canvasBackG = canvasG[0, 0];
									canvasBackB = canvasB[0, 0];
									Placed[i] = true; // image has bee placed on canvas
									maxX2 = maxX1 + w - 1;
									maxY2 = maxY1 + h - 1;
									// prevent bottom right from going outside of canvas
									if (maxX2 > canvasX)
										maxX2 = canvasX;
									if (maxY2 > canvasY)
										maxY2 = canvasY;
									setUsedPixels(maxX1, maxY1, maxX2, maxY2); // mark pixels used
									// update Active Canvas
									if (maxX1 < activeCanvasX1)
										activeCanvasX1 = maxX1;
									if (maxY1 < activeCanvasY1)
										activeCanvasY1 = maxY1;
									if (maxX2 > activeCanvasX2)
										activeCanvasX2 = maxX2;
									if (maxY2 > activeCanvasY2)
										activeCanvasY2 = maxY2;
									imagePlaced = true;
									if (shortCut == 1) {
										lastImageX1 = maxX1;
										lastImageY1 = maxY1;
										lastImageX2 = maxX2;
										lastImageY2 = maxY2;
									}
								}
							}
						}
					} while (imagePlaced); // repeat until no images are placed
				}
			}
		}
	}
}
