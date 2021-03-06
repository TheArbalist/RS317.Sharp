namespace Rs317.Sharp
{
	public class Animable : Cacheable
	{
		public VertexNormal[] vertexNormals;
		public int modelHeight;

		protected Animable()
		{
			modelHeight = 1000;
		}

		public virtual Model getRotatedModel()
		{
			return null;
		}

		public virtual void renderAtPoint(int i, int j, int k, int l, int i1, int j1, int k1, int l1, int i2)
		{
			Model model = getRotatedModel();
			if(model != null)
			{
				modelHeight = model.modelHeight;
				model.renderAtPoint(i, j, k, l, i1, j1, k1, l1, i2);
			}
		}
	}
}