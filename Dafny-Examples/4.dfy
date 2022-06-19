method sum(i: int) returns (sum: int)

	requires i >= 0
	ensures sum == i + 1
{

	sum := i + 2;
	sum := sum + 1;
}
