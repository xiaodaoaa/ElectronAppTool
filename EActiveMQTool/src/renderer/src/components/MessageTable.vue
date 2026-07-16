<template>
  <div ref="containerRef" class="message-table-container">
    <ElTableV2
      v-if="tableSize.width > 0 && tableSize.height > 0"
      :columns="columns"
      :data="consumerStore.messages"
      :width="tableSize.width"
      :height="tableSize.height"
      :row-height="36"
      fixed
    />
  </div>

  <MessageDetail v-model:visible="detailVisible" :message="detailMessage" />
</template>

<script setup lang="ts">
import { ref, computed, h, onMounted, onUnmounted } from "vue";
import { ElTableV2 } from "element-plus";
import { ElButton, ElMessage } from "element-plus";
import MessageDetail from "./MessageDetail.vue";
import { useConsumerStore } from "../stores/consumer";
import { useSettingsStore } from "../stores/settings";
import type { ConsumedMessage } from "../../../shared/types";
import type { Column } from "element-plus";
import type { CellRendererParams } from "element-plus/es/components/table-v2/src/types";
import { FixedDir } from "element-plus/es/components/table-v2/src/constants";

const consumerStore = useConsumerStore();
const settingsStore = useSettingsStore();
const isAutoAck = computed(() => consumerStore.ackMode === 'auto');

const detailVisible = ref(false);
const detailMessage = ref<ConsumedMessage | null>(null);

const containerRef = ref<HTMLElement | null>(null);
const tableSize = ref({ width: 0, height: 0 });

let resizeObserver: ResizeObserver | null = null;

onMounted(() => {
  if (containerRef.value) {
    tableSize.value = {
      width: containerRef.value.clientWidth,
      height: containerRef.value.clientHeight,
    };
    resizeObserver = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (entry) {
        tableSize.value = {
          width: entry.contentRect.width,
          height: entry.contentRect.height,
        };
      }
    });
    resizeObserver.observe(containerRef.value);
  }
});

onUnmounted(() => {
  resizeObserver?.disconnect();
});

function truncateContent(content: string): string {
  const max = settingsStore.maxDisplayLength;
  return content.length > max ? content.slice(0, max) + "..." : content;
}

function onView(row: ConsumedMessage) {
  detailMessage.value = row;
  detailVisible.value = true;
}

async function onAck(row: ConsumedMessage) {
  const r = await consumerStore.ack(row.messageId);
  if (r.success) ElMessage.success("已确认");
  else ElMessage.error(r.error || "确认失败");
}

async function onNack(row: ConsumedMessage) {
  const r = await consumerStore.nack(row.messageId);
  if (r.success) ElMessage.success("已拒绝");
  else ElMessage.error(r.error || "拒绝失败");
}

const columns: Column<ConsumedMessage>[] = [
  {
    key: "seq",
    dataKey: "seq",
    title: "序号",
    width: 70,
    align: "center",
  },
  {
    key: "receivedAt",
    dataKey: "receivedAt",
    title: "接收时间",
    width: 180,
  },
  {
    key: "destination",
    dataKey: "destination",
    title: "目标",
    width: 200,
  },
  {
    key: "messageId",
    dataKey: "messageId",
    title: "消息ID",
    width: 200,
  },
  {
    key: "body",
    dataKey: "body",
    title: "消息体",
    width: 600,
    flexGrow: 1,
    minWidth: 120,
    cellRenderer: ({ cellData }: CellRendererParams<ConsumedMessage>) => {
      const text = String(cellData ?? "");
      return h(
        "span",
        {
          title: text,
          style: {
            display: "block",
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
          },
        },
        truncateContent(text),
      );
    },
  },
  {
    key: "actions",
    dataKey: "messageId",
    title: "操作",
    width: 200,
    fixed: FixedDir.RIGHT,
    cellRenderer: ({ rowData }: CellRendererParams<ConsumedMessage>) => {
      const children = [
        h(
          ElButton,
          {
            size: "small",
            link: true,
            onClick: () => onView(rowData as ConsumedMessage),
          },
          () => "详情",
        ),
      ];
      if (!isAutoAck.value) {
        children.push(
          h(
            ElButton,
            {
              size: "small",
              link: true,
              type: "success",
              onClick: () => onAck(rowData as ConsumedMessage),
            },
            () => "确认",
          ),
          h(
            ElButton,
            {
              size: "small",
              link: true,
              type: "danger",
              onClick: () => onNack(rowData as ConsumedMessage),
            },
            () => "拒绝",
          ),
        );
      }
      return h("div", { style: { display: "flex", gap: "4px" } }, children);
    },
  },
];
</script>

<style scoped>
.message-table-container {
  height: 100%;
  width: 100%;
  min-height: 200px;
}
</style>