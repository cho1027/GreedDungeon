using System;
using System.Collections.Generic;

/// <summary>
/// 힙 정렬 (오름차순, 제자리 정렬).
///  1) 배열을 최대 힙으로 만든다 (마지막 부모 노드부터 거꾸로 SiftDown)
///  2) 맨 앞(가장 큰 값)을 맨 뒤와 바꾸고, 힙 크기를 1 줄인 뒤 다시 SiftDown
///  3) 힙 크기가 1이 될 때까지 반복하면 오름차순 정렬 완료
/// 시간 복잡도 O(n log n), 추가 메모리 O(1).
/// 인덱스 i 의 자식은 2i+1, 2i+2 이다.
/// </summary>
public static class HeapSort
{
    public static void Sort<T>(IList<T> list, Comparison<T> compare)
    {
        if (list == null || compare == null) return;

        int n = list.Count;

        // 1) 최대 힙 만들기
        for (int i = n / 2 - 1; i >= 0; i--)
            SiftDown(list, i, n, compare);

        // 2) 가장 큰 값을 하나씩 뒤로 보내기
        for (int end = n - 1; end > 0; end--)
        {
            Swap(list, 0, end);
            SiftDown(list, 0, end, compare);
        }
    }

    /// <summary>root 노드를 자식들과 비교하며 힙 조건(부모 >= 자식)이 맞을 때까지 내린다</summary>
    private static void SiftDown<T>(IList<T> list, int root, int size, Comparison<T> compare)
    {
        while (true)
        {
            int largest = root;
            int left = root * 2 + 1;
            int right = left + 1;

            if (left < size && compare(list[left], list[largest]) > 0) largest = left;
            if (right < size && compare(list[right], list[largest]) > 0) largest = right;

            if (largest == root) return;

            Swap(list, root, largest);
            root = largest;
        }
    }

    private static void Swap<T>(IList<T> list, int a, int b)
    {
        T tmp = list[a];
        list[a] = list[b];
        list[b] = tmp;
    }
}
