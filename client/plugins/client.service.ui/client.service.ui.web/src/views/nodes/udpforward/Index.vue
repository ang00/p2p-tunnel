<template>
    <div class="forward-wrap">
        <div class="inner">
            <div class="head flex">
                <el-button type="primary" size="small" @click="handleAddListen">增加udp转发监听</el-button>
                <el-button size="small" @click="getData">刷新列表</el-button>
            </div>
            <div class="content">
                <el-row>
                    <template v-for="(item,index) in list" :key="index">
                        <el-col :xs="24" :sm="12" :md="12" :lg="12" :xl="12">
                            <div class="item">
                                <dl>
                                    <dt class="flex">
                                        <span>长连接</span>
                                        <span class="flex-1 t-c">0.0.0.0:{{item.Port}}</span>
                                        <span>
                                            <el-switch size="small" @click.stop @change="onListeningChange(item)" v-model="item.Listening" style="margin-top:-6px;"></el-switch>
                                        </span>
                                    </dt>
                                    <dd>{{item.Desc}}</dd>
                                    <dd>
                                        【{{item.Name}}】{{item.TargetIp}}:{{item.TargetPort}}
                                    </dd>
                                    <dd class="btns t-r">
                                        <el-popconfirm title="删除不可逆，是否确认" @confirm="handleRemoveListen(item)">
                                            <template #reference>
                                                <el-button plain type="danger" size="small">删除</el-button>
                                            </template>
                                        </el-popconfirm>
                                        <el-button plain type="info" size="small" @click="handleEditListen(item)">编辑</el-button>
                                    </dd>
                                </dl>
                            </div>
                        </el-col>
                    </template>
                </el-row>
            </div>
            <AddListen v-if="showAddListen" v-model="showAddListen" @success="getData"></AddListen>
        </div>
    </div>
</template>
<script>
import { reactive, ref, toRefs } from '@vue/reactivity'
import { getList, removeListen, startListen, stopListen } from '../../../apis/udp-forward'
import { onMounted, provide } from '@vue/runtime-core'
import AddListen from './AddListen.vue'
import { injectShareData } from '../../../states/shareData'
import plugin from './plugin'
export default {
    plugin: plugin,
    components: { AddListen },
    setup() {

        const shareData = injectShareData();
        const state = reactive({
            loading: false,
            list: [],
            showAddListen: false,
        });

        const getData = () => {
            getList().then((res) => {
                state.list = res;
            });
        };

        const addListenData = ref({ ID: 0 });
        provide('add-listen-data', addListenData);
        const handleAddListen = () => {
            addListenData.value = { ID: 0 };
            state.showAddListen = true;
        }
        const handleEditListen = (row) => {
            addListenData.value = row;
            state.showAddListen = true;
        }

        const handleRemoveListen = (row) => {
            removeListen(row.Port).then(() => {
                getData();
            });
        }
        const onListeningChange = (row) => {
            if (!row.Listening) {
                stopListen(row.Port).then(getData).catch(getData);
            } else {
                startListen(row.Port).then(getData).catch(getData);
            }
        }
        onMounted(() => {
            getData();
        });

        return {
            ...toRefs(state), shareData, getData,
            handleRemoveListen, handleAddListen, handleEditListen, onListeningChange
        }
    }
}
</script>
<style lang="stylus" scoped>
@media screen and (max-width: 500px) {
    .el-col-24 {
        max-width: 100%;
        flex: 0 0 100%;
    }
}

.forward-wrap {
    padding: 2rem;

    .inner {
        border: 1px solid #eee;
        // padding: 1rem;
        border-radius: 0.4rem;
    }

    .head {
        padding: 1rem;
        border-bottom: 1px solid #eee;
    }

    .content {
        padding: 1rem;

        .item {
            padding: 1rem 0.6rem;

            dl {
                border: 1px solid #eee;
                border-radius: 0.4rem;

                dt {
                    border-bottom: 1px solid #eee;
                    padding: 1rem;
                    font-size: 1.4rem;
                    font-weight: 600;
                    color: #555;
                    line-height: 2.4rem;
                }

                dd {
                    padding: 0.4rem 1rem;

                    &:nth-child(2) {
                        padding: 1rem;
                        background-color: #fafafa;
                        border-bottom: 1px solid #eee;
                    }
                }
            }
        }
    }

    .alert {
        margin-top: 1rem;
    }
}
</style>