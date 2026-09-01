import { useEffect, useRef } from 'react';
import * as echarts from 'echarts/core';
import { LineChart } from 'echarts/charts';
import { GridComponent, LegendComponent, TooltipComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import type { OverviewStats } from '@/shared/api/types';

echarts.use([LineChart, GridComponent, LegendComponent, TooltipComponent, CanvasRenderer]);

export function TrendChart({ data }: { data: OverviewStats['trend'] }) {
  const chartRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!chartRef.current) return;
    const chart = echarts.init(chartRef.current);
    chart.setOption({
      animation: !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
      grid: { left: 2, right: 12, top: 32, bottom: 4, containLabel: true },
      tooltip: {
        trigger: 'axis',
        backgroundColor: '#101828',
        borderWidth: 0,
        textStyle: {
          color: '#fff',
          fontFamily: 'IBM Plex Mono, SFMono-Regular, monospace',
          fontSize: 12,
        },
        formatter: (params: Array<{ axisValue: string; seriesName: string; value: number }>) => {
          const point = params[0];
          const row = data.find((item) => item.label === point.axisValue);
          if (!row) return point.axisValue;
          return `<div style="font-family:IBM Plex Mono,monospace;font-size:12px"><strong>${point.axisValue}</strong><br/>总量 ${row.total.toLocaleString()}<br/>不通过 ${row.reject.toLocaleString()}<br/>建议复核 ${row.review.toLocaleString()}</div>`;
        },
      },
      legend: {
        show: true,
        top: 0,
        right: 0,
        itemWidth: 8,
        itemHeight: 8,
        icon: 'circle',
        textStyle: { color: '#667085', fontSize: 11 },
        data: ['总量', '建议复核'],
      },
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: data.map((item) => item.label),
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: '#98A2B3', fontSize: 11, margin: 14 },
      },
      yAxis: { type: 'value', show: false },
      series: [
        {
          name: '总量',
          type: 'line',
          data: data.map((item) => item.total),
          smooth: 0.35,
          symbol: 'none',
          lineStyle: { width: 2.5, color: '#127C72' },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0,
              y: 0,
              x2: 0,
              y2: 1,
              colorStops: [
                { offset: 0, color: 'rgba(18,124,114,0.16)' },
                { offset: 1, color: 'rgba(18,124,114,0)' },
              ],
            },
          },
        },
        {
          name: '建议复核',
          type: 'line',
          data: data.map((item) => item.review),
          smooth: 0.35,
          symbol: 'none',
          lineStyle: { width: 1.5, color: '#D8933D', type: 'dashed' },
        },
      ],
    });

    const resizeObserver = new ResizeObserver(() => chart.resize());
    resizeObserver.observe(chartRef.current);
    return () => {
      resizeObserver.disconnect();
      chart.dispose();
    };
  }, [data]);

  return <div ref={chartRef} className="trend-chart" aria-label="今日审核量趋势图" />;
}
