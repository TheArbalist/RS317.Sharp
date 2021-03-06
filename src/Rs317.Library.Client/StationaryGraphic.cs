namespace Rs317.Sharp
{
	public sealed class StationaryGraphic : Animable
	{
		public int z;

		public int x;

		public int y;

		public int drawHeight;
		public int stationaryGraphicLoopCycle;
		public bool transformationCompleted;
		private SpotAnimation animation;
		private int elapsedFrames;
		private int duration;

		public StationaryGraphic(int x, int y, int z, int drawHeight, int animationIndex, int loopCycle,
			int loopCycleOffset)
		{
			transformationCompleted = false;
			animation = SpotAnimation.cache[animationIndex];
			this.z = z;
			this.x = x;
			this.y = y;
			this.drawHeight = drawHeight;
			this.stationaryGraphicLoopCycle = loopCycle + loopCycleOffset;
			transformationCompleted = false;
		}

		public void animationStep(int i)
		{
			for (duration += i; duration > animation.sequences.getFrameLength(elapsedFrames);)
			{
				duration -= animation.sequences.getFrameLength(elapsedFrames) + 1;
				elapsedFrames++;
				if (elapsedFrames >= animation.sequences.frameCount
				    && (elapsedFrames < 0 || elapsedFrames >= animation.sequences.frameCount))
				{
					elapsedFrames = 0;
					transformationCompleted = true;
				}
			}

		}

		public override Model getRotatedModel()
		{
			Model model = animation.getModel();
			if (model == null)
				return null;
			int frame = animation.sequences.primaryFrames[elapsedFrames];
			Model animatedModel = new Model(true, Animation.isNullFrame(frame), false, model);
			if (!transformationCompleted)
			{
				animatedModel.createBones();
				animatedModel.applyTransformation(frame);
				animatedModel.triangleSkin = null;
				animatedModel.vertexSkin = null;
			}

			if (animation.scaleXY != 128 || animation.scaleZ != 128)
				animatedModel.scaleT(animation.scaleXY, animation.scaleXY, animation.scaleZ);
			if (animation.rotation != 0)
			{
				if (animation.rotation == 90)
					animatedModel.rotate90Degrees();
				if (animation.rotation == 180)
				{
					animatedModel.rotate90Degrees();
					animatedModel.rotate90Degrees();
				}

				if (animation.rotation == 270)
				{
					animatedModel.rotate90Degrees();
					animatedModel.rotate90Degrees();
					animatedModel.rotate90Degrees();
				}
			}

			animatedModel.applyLighting(64 + animation.modelLightFalloff, 850 + animation.modelLightAmbient, -30, -50, -30,
				true);
			return animatedModel;
		}
	}
}
