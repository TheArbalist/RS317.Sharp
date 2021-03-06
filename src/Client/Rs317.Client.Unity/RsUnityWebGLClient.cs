﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Rs317.Sharp
{
	public sealed class RsUnityWebGLClient : RsUnityClient
	{
		private MonoBehaviour ClientMonoBehaviour { get; }

		public RsUnityWebGLClient(ClientConfiguration config, UnityRsGraphics graphicsObject, [NotNull] MonoBehaviour clientMonoBehaviour) 
			: base(config, graphicsObject)
		{
			if (config == null) throw new ArgumentNullException(nameof(config));

			ClientMonoBehaviour = clientMonoBehaviour ?? throw new ArgumentNullException(nameof(clientMonoBehaviour));

			//Only need to override this for WebGL.
			Sprite.ExternalLoadImageHook += ExternalLoadImageHook;
		}

		private LoadedImagePixels ExternalLoadImageHook(byte[] arg)
		{
			//LoadImage will replace with with incoming image size.
			Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
			ImageConversion.LoadImage(tex, arg);
			tex = FlipTexture(tex);
			int[] pixels = tex.GetPixels32().Select(ColorUtils.ColorToRGB).ToArray();

			return new LoadedImagePixels(tex.height, tex.width, pixels);
		}

		Texture2D FlipTexture(Texture2D original)
		{
			Texture2D flipped = new Texture2D(original.width, original.height);

			int xN = original.width;
			int yN = original.height;

			for(int i = 0; i < xN; i++)
			{
				for(int j = 0; j < yN; j++)
				{
					flipped.SetPixel(i, yN - j - 1, original.GetPixel(i, j));
				}
			}
			flipped.Apply();

			return flipped;
		}

		protected override IRSGraphicsProvider<UnityRsGraphics> CreateGraphicsProvider()
		{
			return new UnityRsGraphicsProvider(GraphicsObject);
		}

		protected override BaseRsImageProducer<UnityRsGraphics> CreateNewImageProducer(int xSize, int ySize, string producerName)
		{
			return new UnityRsImageProducer(xSize, ySize, producerName, gameGraphics);
		}

		public override void StartRunnable(IRunnable runnable, int priority)
		{
			Debug.Log($"WebGL Runnable Called: {runnable.GetType().Name}");
			//Webgl doesn't support threading very well, if at all
			//so we need to handle runnables differently
			ClientMonoBehaviour.StartCoroutine(RunnableRunCoroutine(runnable));
		}

		public override void createClientFrame(int width, int height)
		{
			this.width = width;
			this.height = height;

			signlink.applet = this;
			gameGraphics = CreateGraphicsProvider();
			fullGameScreen = CreateNewImageProducer(this.width, height, nameof(fullGameScreen));

			run();
		}

		private IEnumerator RunnableRunCoroutine(IRunnable runnable)
		{
			yield return new WaitForEndOfFrame();

			//Hope for the best, how it doesn't run infinitely or Unity will break.
			runnable.run();
		}

		protected override void StartOnDemandFetcher(Archive archiveVersions)
		{
			//Webgl requires a non-blocking archive loading system.
			/*onDemandFetcher = new OnDemandFetcher();
			onDemandFetcher.start(archiveVersions, this);*/
			WebGLOnDemandFetcher fetcher = new WebGLOnDemandFetcher();
			onDemandFetcher = fetcher;
			fetcher.Initialize(archiveVersions, this);
			ClientMonoBehaviour.StartCoroutine(fetcher.RunCoroutine());
		}

		protected override void StartFlameDrawing()
		{
			shouldDrawFlames = true;
			currentlyDrawingFlames = true;
			ClientMonoBehaviour.StartCoroutine(DrawFlamesCoroutine());
		}

		public override void run()
		{
			if(shouldDrawFlames)
			{
				ClientMonoBehaviour.StartCoroutine(DrawFlamesCoroutine());
			}
			else
			{
				//If it's webgl, we need to implement it on the main thread
				//as a coroutine. Otherwise WebGL will just not work due to
				//threading limitations.
				ClientMonoBehaviour.StartCoroutine(AppletRunCoroutine());
			}
		}

		private IEnumerator DrawFlamesCoroutine()
		{
			Debug.Log($"Starting flame drawing.");
			drawingFlames = true;

			long startTime = TimeService.CurrentTimeInMilliseconds();
			int currentLoop = 0;
			int interval = 20;
			while(currentlyDrawingFlames)
			{
				flameCycle++;
				calcFlamesPosition();
				yield return null; //these exist to reduce the greed on the main thread in WebGL.
				calcFlamesPosition();
				yield return null; //these exist to reduce the greed on the main thread in WebGL.
				doFlamesDrawing();
				yield return null; //these exist to reduce the greed on the main thread in WebGL.
				if(++currentLoop > 10)
				{
					long currentTime = TimeService.CurrentTimeInMilliseconds();
					int difference = (int)(currentTime - startTime) / 10 - interval;
					interval = 40 - difference;
					if(interval < 5)
						interval = 5;
					currentLoop = 0;
					startTime = currentTime;
				}

				yield return new WaitForSeconds((float)interval / 1000.0f);
			}

			drawingFlames = false;
		}

		public override void startUp()
		{
			if (!RsUnityPlatform.isWebGLBuild)
				base.startUp();
			else
				ClientMonoBehaviour.StartCoroutine(StartupCoroutine());
		}

		public IEnumerator StartupCoroutine()
		{
			if(!wasClientStartupCalled)
				wasClientStartupCalled = true;
			else
				throw new InvalidOperationException($"Failed. Cannot call startup on Client multiple times.");

			drawLoadingText(20, "Starting up");

			if(clientRunning)
			{
				rsAlreadyLoaded = true;
				yield break;
			}

			clientRunning = true;
			bool validHost = true;
			String s = getDocumentBaseHost();
			if(s.EndsWith("jagex.com"))
				validHost = true;
			if(s.EndsWith("runescape.com"))
				validHost = true;
			if(s.EndsWith("192.168.1.2"))
				validHost = true;
			if(s.EndsWith("192.168.1.229"))
				validHost = true;
			if(s.EndsWith("192.168.1.228"))
				validHost = true;
			if(s.EndsWith("192.168.1.227"))
				validHost = true;
			if(s.EndsWith("192.168.1.226"))
				validHost = true;
			if(s.EndsWith("127.0.0.1"))
				validHost = true;
			if(!validHost)
			{
				genericLoadingError = true;
				yield break;
			}

			if(signlink.cache_dat != null)
			{
				for(int i = 0; i < 5; i++)
					caches[i] = new FileCache(signlink.cache_dat, signlink.cache_idx[i], i + 1);
			}

			connectServer();
			archiveTitle = requestArchive(1, "title screen", "title", expectedCRCs[1], 25);
			fontSmall = new GameFont("p11_full", archiveTitle, false);
			fontPlain = new GameFont("p12_full", archiveTitle, false);
			fontBold = new GameFont("b12_full", archiveTitle, false);
			GameFont fontFancy = new GameFont("q8_full", archiveTitle, true);
			drawLogo();
			loadTitleScreen();
			Archive archiveConfig = requestArchive(2, "config", "config", expectedCRCs[2], 30);
			Archive archiveInterface = requestArchive(3, "interface", "interface", expectedCRCs[3], 35);
			Archive archiveMedia = requestArchive(4, "2d graphics", "media", expectedCRCs[4], 40);
			Archive archiveTextures = requestArchive(6, "textures", "textures", expectedCRCs[6], 45);
			Archive archiveWord = requestArchive(7, "chat system", "wordenc", expectedCRCs[7], 50);
			Archive archiveSounds = requestArchive(8, "sound effects", "sounds", expectedCRCs[8], 55);
			tileFlags = new byte[4, 104, 104];
			intGroundArray = CollectionUtilities.Create3DJaggedArray<int>(4, 105, 105);
			worldController = new WorldController(intGroundArray);
			for(int z = 0; z < 4; z++)
				currentCollisionMap[z] = new CollisionMap();

			minimapImage = new Sprite(512, 512);
			Archive archiveVersions = requestArchive(5, "update list", "versionlist", expectedCRCs[5], 60);
			drawLoadingText(60, "Connecting to update server");
			StartOnDemandFetcher(archiveVersions);
			Animation.init(onDemandFetcher.getAnimCount());
			Model.init(onDemandFetcher.fileCount(0), onDemandFetcher);

			songChanging = true;
			onDemandFetcher.request(2, nextSong);
			while(onDemandFetcher.immediateRequestCount() > 0)
			{
				processOnDemandQueue();

				yield return new WaitForSeconds(0.1f);

				if(onDemandFetcher.failedRequests > 3)
				{
					loadError();
					yield break;
				}
			}

			drawLoadingText(65, "Requesting animations");
			int fileRequestCount = onDemandFetcher.fileCount(1);
			for(int id = 0; id < fileRequestCount; id++)
				onDemandFetcher.request(1, id);

			while(onDemandFetcher.immediateRequestCount() > 0)
			{
				int remaining = fileRequestCount - onDemandFetcher.immediateRequestCount();
				if(remaining > 0)
					drawLoadingText(65, "Loading animations - " + (remaining * 100) / fileRequestCount + "%");

				try
				{
					processOnDemandQueue();
				}
				catch(Exception e)
				{
					Console.WriteLine($"Failed to process animation demand queue. Reason: {e.Message} \n Stack: {e.StackTrace}");
					signlink.reporterror($"Failed to process animation demand queue. Reason: {e.Message} \n Stack: {e.StackTrace}");
					throw;
				}

				yield return new WaitForSeconds(0.1f);

				if(onDemandFetcher.failedRequests > 3)
				{
					loadError();
					yield break;
				}
			}

			drawLoadingText(70, "Requesting models");
			fileRequestCount = onDemandFetcher.fileCount(0);
			for(int id = 0; id < fileRequestCount; id++)
			{
				int modelId = onDemandFetcher.getModelId(id);
				if((modelId & 1) != 0)
					onDemandFetcher.request(0, id);
			}

			fileRequestCount = onDemandFetcher.immediateRequestCount();
			while(onDemandFetcher.immediateRequestCount() > 0)
			{
				int remaining = fileRequestCount - onDemandFetcher.immediateRequestCount();
				if(remaining > 0)
					drawLoadingText(70, "Loading models - " + (remaining * 100) / fileRequestCount + "%");
				processOnDemandQueue();

				yield return new WaitForSeconds(0.1f);
			}

			GC.Collect();
			if(caches[0] != null)
			{
				drawLoadingText(75, "Requesting maps");
				onDemandFetcher.request(3, onDemandFetcher.getMapId(0, 47, 48));
				yield return null;
				onDemandFetcher.request(3, onDemandFetcher.getMapId(1, 47, 48));
				yield return null;
				onDemandFetcher.request(3, onDemandFetcher.getMapId(0, 48, 48));
				yield return null;
				onDemandFetcher.request(3, onDemandFetcher.getMapId(1, 48, 48));
				yield return null;
				onDemandFetcher.request(3, onDemandFetcher.getMapId(0, 49, 48));
				yield return null;
				onDemandFetcher.request(3, onDemandFetcher.getMapId(1, 49, 48));
				yield return null;
				onDemandFetcher.request(3, onDemandFetcher.getMapId(0, 47, 47));
				yield return null;
				onDemandFetcher.request(3, onDemandFetcher.getMapId(1, 47, 47));
				yield return null;
				onDemandFetcher.request(3, onDemandFetcher.getMapId(0, 48, 47));
				yield return null;
				onDemandFetcher.request(3, onDemandFetcher.getMapId(1, 48, 47));
				yield return null;
				onDemandFetcher.request(3, onDemandFetcher.getMapId(0, 48, 148));
				yield return null;
				onDemandFetcher.request(3, onDemandFetcher.getMapId(1, 48, 148));
				yield return null;
				fileRequestCount = onDemandFetcher.immediateRequestCount();
				while(onDemandFetcher.immediateRequestCount() > 0)
				{
					int remaining = fileRequestCount - onDemandFetcher.immediateRequestCount();
					if(remaining > 0)
						drawLoadingText(75, "Loading maps - " + (remaining * 100) / fileRequestCount + "%");
					processOnDemandQueue();

					yield return new WaitForSeconds(0.1f);
				}
			}

			fileRequestCount = onDemandFetcher.fileCount(0);
			for(int id = 0; id < fileRequestCount; id++)
			{
				int modelId = onDemandFetcher.getModelId(id);
				byte priority = 0;
				if((modelId & 8) != 0)
					priority = 10;
				else if((modelId & 0x20) != 0)
					priority = 9;
				else if((modelId & 0x10) != 0)
					priority = 8;
				else if((modelId & 0x40) != 0)
					priority = 7;
				else if((modelId & 0x80) != 0)
					priority = 6;
				else if((modelId & 2) != 0)
					priority = 5;
				else if((modelId & 4) != 0)
					priority = 4;
				if((modelId & 1) != 0)
					priority = 3;
				if(priority != 0)
					onDemandFetcher.setPriority(priority, 0, id);
			}

			onDemandFetcher.preloadRegions(membersWorld);

			//Remove low memory check.
			int count = onDemandFetcher.fileCount(2);
			for(int id = 1; id < count; id++)
				if(onDemandFetcher.midiIdEqualsOne(id))
					onDemandFetcher.setPriority((byte)1, 2, id);

			drawLoadingText(80, "Unpacking media");

			//Default cache can cause some errors, so wecan surpress them here.
			try
			{
				InitializeUnpackedMedia(archiveMedia);
			}
			catch (Exception e)
			{
				Console.WriteLine($"Media Error: {e}");
			}

			drawLoadingText(83, "Unpacking textures");
			Rasterizer.unpackTextures(archiveTextures);
			Rasterizer.calculatePalette(0.80000000000000004D);
			Rasterizer.resetTextures();
			drawLoadingText(86, "Unpacking config");
			AnimationSequence.unpackConfig(archiveConfig);
			GameObjectDefinition.load(archiveConfig);
			FloorDefinition.load(archiveConfig);
			ItemDefinition.load(archiveConfig);
			EntityDefinition.load(archiveConfig);
			IdentityKit.load(archiveConfig);
			SpotAnimation.load(archiveConfig);
			Varp.load(archiveConfig);
			VarBit.load(archiveConfig);
			ItemDefinition.membersWorld = membersWorld;

			//Removed low memory check
			drawLoadingText(90, "Unpacking sounds");
			byte[] soundData = archiveSounds.decompressFile("sounds.dat");
			Default317Buffer stream = new Default317Buffer(soundData);
			Effect.load(stream);

			drawLoadingText(95, "Unpacking interfaces");
			GameFont[] fonts = { fontSmall, fontPlain, fontBold, fontFancy };
			RSInterface.unpack(archiveInterface, fonts, archiveMedia);
			drawLoadingText(100, "Preparing game engine");
			for(int _y = 0; _y < 33; _y++)
			{
				int firstXOfLine = 999;
				int lastXOfLine = 0;
				for(int _x = 0; _x < 34; _x++)
				{
					if(minimapBackgroundImage.pixels[_x + _y * minimapBackgroundImage.width] == 0)
					{
						if(firstXOfLine == 999)
							firstXOfLine = _x;
						continue;
					}

					if(firstXOfLine == 999)
						continue;
					lastXOfLine = _x;
					break;
				}

				compassHingeSize[_y] = firstXOfLine;
				compassWidthMap[_y] = lastXOfLine - firstXOfLine;
			}

			for(int _y = 5; _y < 156; _y++)
			{
				int min = 999;
				int max = 0;
				for(int _x = 25; _x < 172; _x++)
				{
					if(minimapBackgroundImage.pixels[_x + _y * minimapBackgroundImage.width] == 0
						&& (_x > 34 || _y > 34))
					{
						if(min == 999)
							min = _x;
						continue;
					}

					if(min == 999)
						continue;
					max = _x;
					break;
				}

				minimapLeft[_y - 5] = min - 25;
				minimapLineWidth[_y - 5] = max - min;
			}

			Rasterizer.setBounds(479, 96);
			chatboxLineOffsets = Rasterizer.lineOffsets;
			Rasterizer.setBounds(190, 261);
			sidebarOffsets = Rasterizer.lineOffsets;
			Rasterizer.setBounds(512, 334);
			viewportOffsets = Rasterizer.lineOffsets;

			int[] ai = new int[9];
			for(int i8 = 0; i8 < 9; i8++)
			{
				int k8 = 128 + i8 * 32 + 15;
				int l8 = 600 + k8 * 3;
				int i9 = Rasterizer.SINE[k8];
				ai[i8] = l8 * i9 >> 16;
			}

			WorldController.setupViewport(500, 800, 512, 334, ai);

			GameObject.clientInstance = this;
			GameObjectDefinition.clientInstance = this;
			EntityDefinition.clientInstance = this;

			//TODO: Disabled censor
			//Censor.load(archiveWord);
			mouseDetection = new MouseDetection(this);
			//startRunnable(mouseDetection, 10);
			yield break;
		}

		private IEnumerator AppletRunCoroutine()
		{
			Console.WriteLine($"Loading.");
			drawLoadingText(0, "Loading...");
			yield return StartupCoroutine();
			int opos = 0;
			int ratio = 256;
			int delay = 1;
			int count = 0;
			int intex = 0;
			for(int otim = 0; otim < 10; otim++)
				otims[otim] = TimeService.CurrentTimeInMilliseconds();

			while(gameState >= 0)
			{
				if(gameState > 0)
				{
					gameState--;
					if(gameState == 0)
					{
						exit();
						yield break;
					}
				}

				int i2 = ratio;
				int j2 = delay;
				ratio = 300;
				delay = 1;
				long currentTime = TimeService.CurrentTimeInMilliseconds();
				if(otims[opos] == 0L)
				{
					ratio = i2;
					delay = j2;
				}
				else if(currentTime > otims[opos])
					ratio = (int)(2560 * delayTime / (currentTime - otims[opos]));

				if(ratio < 25)
					ratio = 25;
				if(ratio > 256)
				{
					ratio = 256;
					delay = (int)(delayTime - (currentTime - otims[opos]) / 10L);
				}

				if(delay > delayTime)
					delay = delayTime;
				otims[opos] = currentTime;
				opos = (opos + 1) % 10;
				if(delay > 1)
				{
					for(int otim = 0; otim < 10; otim++)
						if(otims[otim] != 0L)
							otims[otim] += delay;

				}

				if(delay < minDelay)
					delay = minDelay;

				if(delay > 1)
					//Delay is in milliseconds, so we need to convert to seconds.
					yield return new WaitForSeconds((float)delay / 1000.0f);
				else if(delay == 1)
					yield return new WaitForEndOfFrame();

				for(; count < 256; count += ratio)
				{
					clickType = eventMouseButton;
					clickX = eventClickX;
					clickY = eventClickY;
					clickTime = eventClickTime;
					eventMouseButton = 0;
					processGameLoop();
					readIndex = writeIndex;
				}

				count &= 0xff;
				if(delayTime > 0)
					fps = (1000 * ratio) / (delayTime * 256);
				processDrawing();
				if(debugRequested)
				{
					Console.WriteLine("ntime:" + currentTime);
					for(int i = 0; i < 10; i++)
					{
						int otim = ((opos - i - 1) + 20) % 10;
						Console.WriteLine("otim" + otim + ":" + otims[otim]);
					}

					Console.WriteLine("fps:" + fps + " ratio:" + ratio + " count:" + count);
					Console.WriteLine("del:" + delay + " deltime:" + delayTime + " mindel:" + minDelay);
					Console.WriteLine("intex:" + intex + " opos:" + opos);
					debugRequested = false;
					intex = 0;
				}
			}

			if(gameState == -1)
				exit();
		}
	}
}
