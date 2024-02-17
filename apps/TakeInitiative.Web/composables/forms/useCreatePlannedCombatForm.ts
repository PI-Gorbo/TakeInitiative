export const useCreatePlannedCombatForm = () => {
	
	// Form Definition
	const { values, errors, defineField, validate } = use    Form({
		validationSchema: toTypedSchema(
			yup.object({
				email: yup.string().required().email(),
				password: yup.string().required(),
			})
		),
	});
	const [email, emailInputProps] = defineField("email", {
		props: (state) => ({
			errorMessage: state.errors[0],
		}),
	});
	const [password, passwordInputProps] = defineField("password", {
		props: (state) => ({
			errorMessage: state.errors[0],
		}),
	});

	// Form Submit
	const userStore = useUserStore();
	async function onLogin() {
		state.errorObject = null;
		state.isSubmitting = true;
		const validation = await validate();
		if (validation.valid == false) {
			state.isSubmitting = false;
			return;
		}

		await userStore
			.login({ email: email.value ?? "", password: password.value ?? "" })
			.then(async () => {
				await navigateTo("/");
			})
			.catch(async (error) => {
				state.errorObject = await parseAsApiError(error);
			})
			.finally(() => (state.isSubmitting = false));
	}
}