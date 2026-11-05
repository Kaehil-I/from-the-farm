plugins {
    id("com.android.application") version "8.2.2" apply false
    id("org.jetbrains.kotlin.android") version "1.9.22" apply false
    id("com.google.gms.google-services") version "4.4.2" apply false
    // KSP runs the Room compiler. The version must start with the Kotlin version above.
    id("com.google.devtools.ksp") version "1.9.22-1.0.17" apply false
}

