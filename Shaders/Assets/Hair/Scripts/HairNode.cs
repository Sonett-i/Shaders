public struct HairNode
{
	//	2D Position of the point
	public float x;
	public float y;

	//	2D velocity
	public float vx;
	public float vy;

	//	Sum of forces or change of velocity in the current frame.
	public int dvx;
	public int dvy;

	// Make struct divisible by 128 bits
	int dummy1;
	int dummy2;

	/*	Reference
	 *	
	 *	https://developer.nvidia.com/content/understanding-structured-buffer-performance
	 * 
	 */
}
