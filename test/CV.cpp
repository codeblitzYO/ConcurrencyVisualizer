// CV.cpp : コンソール アプリケーションのエントリ ポイントを定義します。
//

#include "stdafx.h"
#include "cvMarker.h"


void test1()
{
	cvMarker::push("test1");

	concurrency::task_group g;

	std::function<void(int)> f = [&](int n) {
		cvMarker::push("f");

		std::this_thread::sleep_for(std::chrono::microseconds(100));
		if (n < 8)
		{
			g.run([&]() { f(++n); });
		}

		cvMarker::pop();
	};

	g.run([&]() { f(0); });
	g.run([&]() { f(0); });
	g.run([&]() { f(0); });
	g.wait();

	cvMarker::pop();
}


void loop()
{
	cvMarker::push("loop");

	test1();

	cvMarker::pop();
}

int main()
{
	while(true)
	{
		loop();
	}

    return 0;
}

