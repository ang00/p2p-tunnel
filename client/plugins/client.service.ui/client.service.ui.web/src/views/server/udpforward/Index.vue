<template>
    <div class="forward-wrap">
        <div class="inner">
            <div class="head flex">
                <el-button type="primary" size="small" @click="handleAddListen">增加udp转发监听</el-button>
                <el-button size="small" @click="getData">刷新列表</el-button>
                <span class="flex-1"></span>
            </div>
            <div class="content">
                <el-row>
                    <template v-for="(item,index) in list" :key="index">
                        <el-col :xs="12" :sm="8" :md="8" :lg="8" :xl="8">
                            <div class="item">
                                <dl>
                                    <dt class="flex">
                                        <span>长连接</span>
                                        <span class="flex-1 t-c">{{signinState.ServerConfig.Ip}}:{{item.ServerPort}}</span>
                                        <span>
                                            <el-switch size="small" @click.stop @change="onListeningChange(item)" v-model="item.Listening" style="margin-top:-6px;"></el-switch>
                                        </span>
                                    </dt>
                                    <dd>{{item.Desc}}</dd>
                                    <dd>
                                        【本机】{{item.LocalIp}}:{{item.LocalPort}}
                                    </dd>
                                    <dd class="btns t-r">
                                        <el-popconfirm title="删除不可逆，是否确认" @confirm="handleRemoveListen(item)">
                                            <template #reference>
                                                <el-button plain type="danger" size="small">删除</el-button>
                                            </template>
                                        </el-popconfirm>
                                    </dd>
                                </dl>
                            </div>
                        </el-col>
                    </template>
                </el-row>
            </div>
        </div>
        <AddListen v-if="showAddListen" v-model="showAddListen" @success="getData"></AddListen>
    </div>
</template>
<script>
import { reactive, ref, toRefs } from '@vue/reactivity'
import { getServerForwards, startServerForward, stopServerForward, removeServerForward } from '../../../apis/udp-forward-server'
import { onMounted, provide } from '@vue/runtime-core'
import AddListen from './AddListen.vue'
import { injectShareData } from '../../../states/shareData'
import { injectSignIn } from '../../../states/signin'
import plugin from './plugin'
export default {
    plugin: plugin,
    components: { AddListen },
    setup() {

        const shareData = injectShareData();
        const signinState = injectSignIn();
        const state = reactive({
            loading: false,
            list: [],
            showAddListen: false,
        });

        const getData = () => {
            getServerForwards().then((res) => {
                state.list = res;
            });
        };

        const addListenData = ref({ ID: 0 });
        provide('add-listen-data', addListenData);
        const handleAddListen = () => {
            addListenData.value = { ID: 0 };
            state.showAddListen = true;
        }
        const handleRemoveListen = (row) => {
            removeServerForward(row.ServerPort).then(() => {
                getData();
            });
        }
        const onListeningChange = (row) => {
            if (row.Listening == false) {
                stopServerForward(row.ServerPort).then(getData).catch(getData);
            } else {
                startServerForward(row.ServerPort).then(getData).catch(getData);
            }
        }
        onMounted(() => {
            getData();
        });

        return {
            ...toRefs(state), signinState, shareData, getData,
            handleRemoveListen, handleAddListen, onListeningChange
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
        border: 1px solid var(--main-border-color);
        // padding: 1rem;
        border-radius: 0.4rem;
    }

    .head {
        padding: 1rem;
        border-bottom: 1px solid var(--main-border-color);
    }

    .content {
        padding: 1rem;

        .item {
            padding: 1rem 0.6rem;

            dl {
                border: 1px solid var(--main-border-color);
                border-radius: 0.4rem;

                dt {
                    border-bottom: 1px solid var(--main-border-color);
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