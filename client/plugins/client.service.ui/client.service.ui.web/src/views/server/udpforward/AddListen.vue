<template>
    <el-dialog :title="form.ID > 0?'编辑监听':'新增监听'" top="1vh" destroy-on-close v-model="show" center :close-on-click-modal="false" width="350px">
        <el-form ref="formDom" :model="form" :rules="rules" label-width="80px">
            <el-form-item label="监听端口" prop="ServerPort">
                <el-input v-model="form.ServerPort" :readonly="form.ID > 0"></el-input>
            </el-form-item>
            <el-form-item label="本地IP" prop="LocalIp">
                <el-input v-model="form.LocalIp"></el-input>
            </el-form-item>
            <el-form-item label="本地端口" prop="LocalPort">
                <el-input v-model="form.LocalPort"></el-input>
            </el-form-item>
            <el-form-item label="简单说明" prop="Desc">
                <el-input v-model="form.Desc"></el-input>
            </el-form-item>
        </el-form>
        <template #footer>
            <el-button @click="handleCancel">取 消</el-button>
            <el-button type="primary" :loading="loading" @click="handleSubmit">确 定</el-button>
        </template>
    </el-dialog>
</template>

<script>
import { reactive, ref, toRefs } from '@vue/reactivity';
import { AddServerForward } from '../../../apis/udp-forward-server'
import { injectShareData } from '../../../states/shareData'
import { injectClients } from '../../../states/clients'
import { inject, watch } from '@vue/runtime-core';
export default {
    props: ['modelValue'],
    emits: ['update:modelValue', 'success'],
    setup(props, { emit }) {
        const addListenData = inject('add-listen-data');
        const shareData = injectShareData();
        const clientsState = injectClients();
        const state = reactive({
            show: props.modelValue,
            loading: false,
            form: {
                ID: addListenData.value.ID || 0,
                ServerPort: addListenData.value.ServerPort || 0,
                LocalIp: addListenData.value.LocalIp || '127.0.0.1',
                LocalPort: addListenData.value.LocalPort || '80',
                Desc: addListenData.value.Desc || '',
            },
            rules: {
                ServerPort: [
                    { required: true, message: '必填', trigger: 'blur' },
                    {
                        type: 'number', min: 1, max: 65535, message: '数字 1-65535', trigger: 'blur', transform(value) {
                            return Number(value)
                        }
                    }
                ],
                LocalIp: [{ required: true, message: '必填', trigger: 'blur' }],
                LocalPort: [
                    { required: true, message: '必填', trigger: 'blur' },
                    {
                        type: 'number', min: 1, max: 65535, message: '数字 1-65535', trigger: 'blur', transform(value) {
                            return Number(value)
                        }
                    }
                ],
            }
        });
        watch(() => state.show, (val) => {
            if (!val) {
                setTimeout(() => {
                    emit('update:modelValue', val);
                }, 300);
            }
        });

        const formDom = ref(null);
        const handleSubmit = () => {
            formDom.value.validate((valid) => {
                if (!valid) {
                    return false;
                }
                state.loading = true;

                const json = JSON.parse(JSON.stringify(state.form));
                json.ID = Number(json.ID);
                json.ServerPort = Number(json.ServerPort);
                json.LocalPort = Number(json.LocalPort);
                AddServerForward(json).then((res) => {
                    state.loading = false;
                    if (res) {
                        state.show = false;
                        emit('success');
                    }
                }).catch((e) => {
                    state.loading = false;
                });
            })
        }
        const handleCancel = () => {
            state.show = false;
        }

        return {
            shareData, ...toRefs(state), ...toRefs(clientsState), formDom, handleSubmit, handleCancel
        }
    }
}
</script>
<style lang="stylus" scoped></style>