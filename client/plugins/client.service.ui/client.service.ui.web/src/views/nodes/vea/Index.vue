<template>
    <div class="socks5-wrap">
        <div class="form">
            <el-form ref="formDom" label-width="0">
                <el-form-item>
                    <div class="w-100 t-c">
                        <ConnectButton :loading="state.loading" v-model="state.enable" @handle="handle"></ConnectButton>
                    </div>
                </el-form-item>
                <el-form-item>
                    <div class="w-100 t-c">
                        <span>目标</span>：<el-select v-model="state.targetName" placeholder="选择目标" @change="handleChange">
                            <el-option v-for="(item,index) in targets" :key="index" :label="item.label" :value="item.Name">
                            </el-option>
                        </el-select>
                    </div>
                </el-form-item>
            </el-form>
        </div>
    </div>
</template>

<script>
import { computed, reactive } from '@vue/reactivity'
import { getConfig, setConfig, runVea } from '../../../apis/vea'
import { onMounted } from '@vue/runtime-core'
import { injectClients } from '../../../states/clients'
import ConnectButton from '../../../components/ConnectButton.vue'
import plugin from './plugin'
export default {
    plugin: plugin,
    components: { ConnectButton },
    setup() {

        const clientsState = injectClients();
        const targets = computed(() => {
            return clientsState.clients.map(c => {
                return { Name: c.Name, label: c.Name }
            });
        });
        const state = reactive({
            loading: false,
            enable: false,
            targetName: ''
        });

        const loadConfig = () => {
            getConfig().then((res) => {
                state.enable = res.Enable;
                state.targetName = res.TargetName;
            });
        }

        onMounted(() => {
            loadConfig();
        });

        const submit = () => {
            state.loading = true;
            getConfig().then((res) => {
                res.targetName = state.targetName;
                res.Enable = state.enable;
                setConfig(res).then(() => {
                    loadConfig();
                    runVea().then(() => {
                        state.loading = false;
                    }).catch(() => {
                        state.loading = false;
                    })
                }).catch(() => {
                    state.loading = false;
                });
            }).catch(() => {
                state.loading = false;
            });
        }
        const handle = () => {
            if (state.loading) return;
            state.enable = !state.enable;
            submit();
        };
        const handleChange = (name) => {
            if (state.loading) return;
            state.targetName = name;
            submit();
        }

        return {
            targets, state, handle, handleChange
        }
    }
}
</script>

<style lang="stylus" scoped>
.socks5-wrap {
    padding: 5rem 1rem 2rem 1rem;
}

.inner {
    border: 1px solid #ddd;
    padding: 1rem;
    border-radius: 0.4rem;
    margin-bottom: 1rem;
}

@media screen and (max-width: 768px) {
    .el-col {
        margin-top: 0.6rem;
    }
}
</style>